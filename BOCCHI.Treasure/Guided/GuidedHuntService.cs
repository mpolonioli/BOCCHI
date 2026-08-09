using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Ocelot.Lifecycle;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     The hand-run counterpart to the automated hunts. It never moves the character: it decides which spawn point is
///     worth walking to next, drops a map flag on it, and marks it off by itself once the player gets close enough to
///     see whether anything is there — so a lap can be run without ever going back to the window.
/// </summary>
public abstract class GuidedHuntService<TSpot>
(
    GuidedSpotTracker<TSpot> tracker,
    IGuidedHuntConfig config,
    GuidedHuntCoordinator coordinator,
    IZoneProvider zones,
    ISubMapResolver subMaps,
    IClientState clientState,
    IObjectTable objects,
    IChatGui chat,
    IPluginLog log
) : IGuidedHunt, IOnUpdate, IOnStart, IOnStop
    where TSpot : GuidedSpot
{
    public GuidedSpotTracker<TSpot> Tracker => tracker;

    /// <summary>How this hunt names itself in chat — "treasure hunt", "carrot hunt".</summary>
    public abstract string Label { get; }

    /// <summary>
    ///     What this hunt is called in the log, and the discriminator for its throttle keys — two hunts sharing a key
    ///     would take each other's ticks.
    /// </summary>
    protected abstract string Key { get; }

    /// <summary>Spots the player has chosen to leave alone this lap. Cleared by a reset.</summary>
    private readonly HashSet<uint> skipped = [];

    /// <summary>True while the target came from the table rather than from <see cref="PickNext" />.</summary>
    private bool targetSetByHand;

    /// <summary>Suggested visiting order, by spot id. Rebuilt when the candidate set changes, not every frame.</summary>
    private List<uint> plan = [];

    private int planSignature;

    /// <summary>True while the service is actively steering — picking targets, moving the flag and announcing.</summary>
    public bool IsGuiding { get; private set; }

    /// <summary>
    ///     The only unprompted flag write is <see cref="MaintainTarget" />'s, so with auto-flag off this hunt never
    ///     takes the marker from anyone.
    /// </summary>
    public bool MovesFlagAutomatically => config.AutoFlagNextTarget;

    public TSpot? Target { get; private set; }

    public Vector3 PlayerPosition => objects.LocalPlayer?.Position ?? Vector3.Zero;

    /// <summary>
    ///     Map the player is standing on, or 0 when the zone is one flat map. Compared against <see cref="GuidedSpot.MapId" />
    ///     to tell a spot that is genuinely nearby from one that is only nearby in a straight line. Read once a tick
    ///     rather than on demand, because the table asks it of every row every frame.
    /// </summary>
    public uint PlayerArea { get; private set; }

    /// <summary>True when this spot is on a different map from the player, so the distance to it means nothing.</summary>
    public bool IsElsewhere(GuidedSpot spot)
    {
        return PlayerArea != 0 && spot.MapId != 0 && spot.MapId != PlayerArea;
    }

    public virtual void OnStart()
    {
        coordinator.Register(this);
        tracker.OnStatusChanged += OnSpotResolved;
    }

    public virtual void OnStop()
    {
        tracker.OnStatusChanged -= OnSpotResolved;
    }

    public void Update()
    {
        if (!config.Enabled)
        {
            return;
        }

        PlayerArea = subMaps.HasSubMaps ? subMaps.MapIdFor(PlayerPosition) : 0u;
        BeforeTick();
        tracker.Tick(config.ObservationRange);
        MaintainPlan();
        MaintainTarget();
    }

    /// <summary>Runs each tick before the spots are observed, for whatever the hunt has to keep current itself.</summary>
    protected virtual void BeforeTick()
    {
    }

    #region Guiding

    public void Start()
    {
        if (IsGuiding)
        {
            return;
        }

        // Before the flag is set, not after: the other hunt clearing its own target must not undo ours.
        coordinator.Claim(this);

        IsGuiding = true;

        // Deliberately not clearing the target: flagging a spot from the table and then pressing Start is a reasonable
        // way to say "begin here", and MaintainTarget picks one anyway if what is held is no longer worth visiting.
        MaintainTarget();
    }

    public void Stop()
    {
        IsGuiding = false;
        Target = null;
        targetSetByHand = false;
    }

    public void Toggle()
    {
        if (IsGuiding)
        {
            Stop();
            return;
        }

        Start();
    }

    /// <summary>Points the hunt at a specific spot, overriding what it would have picked, and flags it.</summary>
    public void SetTarget(TSpot spot, bool flag = true)
    {
        Target = spot;
        targetSetByHand = true;
        skipped.Remove(spot.Id);

        if (flag)
        {
            PlaceFlag(spot);
        }
    }

    /// <summary>
    ///     Chooses the next spot and moves the flag when the current one is no longer worth walking to. The target is
    ///     otherwise sticky: repicking every frame would swap the flag around every time the player's position made
    ///     another spot marginally closer.
    /// </summary>
    protected void MaintainTarget()
    {
        if (!IsGuiding || !zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        // Reference equality against the live list on purpose: a zone change replaces every spot object, and a target
        // held over from the last zone would otherwise still look like a perfectly good candidate.
        if (Target != null && tracker.Spots.Contains(Target) && IsCandidate(Target) && !ShouldLeaveBehind(Target))
        {
            return;
        }

        TSpot? next = PickNext();
        if (next == null)
        {
            Target = null;
            OnNothingLeft();
            return;
        }

        Target = next;
        targetSetByHand = false;

        if (config.AutoFlagNextTarget)
        {
            PlaceFlag(next);
        }

        Vector3 from = PlayerPosition;
        Announce(IsElsewhere(next)
            ? $"Next: {next.Label} in {next.Area ?? "another part of the zone"} — nothing left worth walking to on this map."
            : $"Next: {next.Label}, {next.DistanceTo(from):f0}y {next.BearingFrom(from)}.");
    }

    /// <summary>Called when nothing is left worth walking to, which is not the same thing for every hunt.</summary>
    protected virtual void OnNothingLeft()
    {
    }

    /// <summary>
    ///     Nearest candidate, with the hunt's own bias applied through <see cref="ScoreFor" />. Distance is straight-line
    ///     on purpose: the precomputed graph holds no player-to-node edges, so it cannot answer "which of these is
    ///     closest to me right now".
    ///     <para>
    ///         Which is exactly why the player's own map wins before distance is looked at. A straight line goes through
    ///         the floor: a Subterrane spot 150y under the Dark Territory reads as the closest thing in the zone while
    ///         actually being a run to the entrance and a descent away. Spots on another map are not dropped — they are
    ///         simply not offered until nothing is left up here, which is the order a lap gets run in anyway.
    ///     </para>
    /// </summary>
    protected TSpot? PickNext()
    {
        Vector3 from = PlayerPosition;
        uint area = PlayerArea;

        TSpot? best = null;
        float bestScore = float.MaxValue;
        bool bestIsHere = false;

        foreach(TSpot spot in tracker.Spots)
        {
            if (!IsCandidate(spot))
            {
                continue;
            }

            // Unresolved spots count as here rather than elsewhere, so nothing can be stranded by a map the resolver
            // could not place.
            bool here = area == 0 || spot.MapId == 0 || spot.MapId == area;
            if (bestIsHere && !here)
            {
                continue;
            }

            float score = ScoreFor(spot, spot.DistanceTo(from));

            if (here == bestIsHere && score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestIsHere = here;
            best = spot;
        }

        return best;
    }

    /// <summary>What this spot costs to visit next. Straight-line distance unless the hunt has a reason to prefer one.</summary>
    protected virtual float ScoreFor(TSpot spot, float distance)
    {
        return distance;
    }

    /// <summary>
    ///     True when a target the hunt picked for itself sits on a map the player has since left, while something on
    ///     the map they are on is still worth walking to. Stickiness exists to stop the flag flip-flopping between two
    ///     spots a few yalms apart; it is not meant to hold a target across a descent the player has already undone.
    ///     <para>A target set by hand from the table is never left behind — that one is an instruction, not a guess.</para>
    /// </summary>
    private bool ShouldLeaveBehind(TSpot target)
    {
        if (targetSetByHand || !IsElsewhere(target))
        {
            return false;
        }

        return Candidates().Any(spot => !IsElsewhere(spot));
    }

    /// <summary>Every spot the hunt would still walk to, after the player's own exclusions.</summary>
    public virtual bool IsCandidate(TSpot spot)
    {
        if (!spot.IsWorthVisiting || skipped.Contains(spot.Id))
        {
            return false;
        }

        return !IsAboveMyLevel(spot);
    }

    public IEnumerable<TSpot> Candidates()
    {
        return tracker.Spots.Where(IsCandidate);
    }

    #endregion

    #region Plan

    /// <summary>
    ///     Rebuilds the suggested order when the set of candidates changes. The order is only advice — the player is
    ///     free to take it in any order they like — so it is anchored on the current target rather than on the player,
    ///     which keeps it stable while they walk.
    /// </summary>
    private void MaintainPlan()
    {
        if (!EzThrottler.Throttle($"BOCCHI.Guided.{Key}.Plan", 1000))
        {
            return;
        }

        List<TSpot> candidates = Candidates().ToList();
        uint area = PlayerArea;

        int signature = candidates.Aggregate(candidates.Count, (acc, spot) => HashCode.Combine(acc, spot.Id, (int)spot.Status));
        signature = HashCode.Combine(signature, Target?.Id ?? 0, PlanRevision, area);

        if (signature == planSignature)
        {
            return;
        }

        planSignature = signature;

        if (candidates.Count == 0)
        {
            plan = [];
            return;
        }

        plan = BuildPlan(candidates);
    }

    /// <summary>
    ///     Anything outside the candidate set that changes the order — folded into the plan signature so that flipping
    ///     a setting rebuilds the plan rather than leaving the old one up until a spot resolves.
    /// </summary>
    protected virtual int PlanRevision => 0;

    /// <summary>
    ///     Straight-line order, map first. A hunt with real route distances to hand overrides this; without them the
    ///     map has to be the first sort key, because a straight line goes through the floor.
    /// </summary>
    protected virtual List<uint> BuildPlan(List<TSpot> candidates)
    {
        Vector3 from = PlayerPosition;

        return candidates
            .OrderBy(s => IsElsewhere(s) ? 1 : 0)
            .ThenBy(s => s.DistanceTo(from))
            .Select(s => s.Id)
            .ToList();
    }

    /// <summary>1-based position of a spot in the suggested order, or null when it is not part of the plan.</summary>
    public int? PlanPosition(GuidedSpot spot)
    {
        int index = plan.IndexOf(spot.Id);
        return index < 0 ? null : index + 1;
    }

    #endregion

    #region Actions

    /// <summary>
    ///     Drops the map flag on a spot. Existing flags are cleared first: the game keeps several markers, and leaving
    ///     the old one up makes it ambiguous which one to run at.
    ///     <para>
    ///         The marker is placed on the spot's own map, not the one currently open. Flagging a Subterrane spot
    ///         against the surface map puts the flag at the coordinates it would have up there, which is a different
    ///         place entirely — a flag on the wrong map is worse than no flag, because it still looks like a destination.
    ///     </para>
    /// </summary>
    public unsafe void PlaceFlag(GuidedSpot spot)
    {
        AgentMap* map = AgentMap.Instance();
        if (map == null)
        {
            return;
        }

        map->FlagMarkerCount = 0;
        map->SetFlagMapMarker(clientState.TerritoryType, subMaps.MapIdFor(spot.Position), spot.Position);
    }

    public bool IsSkipped(GuidedSpot spot)
    {
        return skipped.Contains(spot.Id);
    }

    public void ToggleSkip(TSpot spot)
    {
        if (!skipped.Add(spot.Id))
        {
            skipped.Remove(spot.Id);
            return;
        }

        if (ReferenceEquals(Target, spot))
        {
            Target = null;
            targetSetByHand = false;
        }
    }

    public virtual void ResetChecks()
    {
        tracker.ResetChecks();
        skipped.Clear();
        Target = null;
        targetSetByHand = false;
        planSignature = 0;
    }

    /// <summary>Mobs around this spot are at or above the player's Knowledge Level, so they will aggro on the way in.</summary>
    public bool IsAboveMyLevel(GuidedSpot spot)
    {
        if (!config.HideSpotsAboveMyLevel || spot.Level == 0)
        {
            return false;
        }

        uint level = KnowledgeLevel();
        return level > 0 && spot.Level > level;
    }

    public unsafe uint KnowledgeLevel()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return 0u;
        }

        FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara* chara =
            (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)player.Address;

        return chara == null ? 0u : (uint)chara->ForayInfo.Level;
    }

    #endregion

    #region Reporting

    private void OnSpotResolved(TSpot spot, SpotStatus previous)
    {
        string? outcome = DescribeOutcome(spot, previous);
        if (outcome == null)
        {
            return;
        }

        Announce($"{spot.Label} {outcome}.");
    }

    /// <summary>
    ///     What to say about a status change, or null to say nothing. Only the first resolution of a spot is worth a
    ///     line by default — the churn after that is the hunt's own bookkeeping, not news.
    /// </summary>
    protected virtual string? DescribeOutcome(TSpot spot, SpotStatus previous)
    {
        if (previous != SpotStatus.Unknown)
        {
            return null;
        }

        return spot.Status switch
        {
            SpotStatus.Present => "is there",
            SpotStatus.Opened => "was already opened",
            var _ => "is empty"
        };
    }

    protected void Announce(string message)
    {
        log.Debug("[Guided {Key}] {Message}", Key, message);

        if (IsGuiding && config.AnnounceInChat)
        {
            chat.Print($"[BOCCHI] {message}");
        }
    }

    #endregion
}
