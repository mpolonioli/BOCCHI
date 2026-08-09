using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Treasure.Services;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     The hand-run counterpart to <see cref="TreasureHunterService" />: it points the player at coffer spawn points and
///     marks them off as they pass, but never moves the character.
///     <para>
///         What it adds over the shared machinery is arithmetic. Treasure Sight reports how many coffers of each tier
///         are spawned across the instance, and that number against the ones already located is what says whether there
///         is any point continuing to search — and, when it goes up, that a respawn wave has invalidated the lap.
///     </para>
/// </summary>
public class GuidedTreasureService
(
    GuidedCofferTracker tracker,
    GuidedRouteAdvisor advisor,
    ITreasureTracker treasure,
    GuidedHuntCoordinator coordinator,
    IZoneProvider zones,
    ISubMapResolver subMaps,
    IClientState clientState,
    IObjectTable objects,
    IChatGui chat,
    IPluginLog log,
    GuidedTreasureConfig config
) : GuidedHuntService<CofferSpot>(tracker, config, coordinator, zones, subMaps, clientState, objects, chat, log)
{
    public GuidedCofferTracker Coffers => tracker;

    public GuidedRouteAdvisor Advisor => advisor;

    public GuidedTreasureConfig Config => config;

    public override string Label => "treasure hunt";

    protected override string Key => "treasure";

    /// <summary>The previous Treasure Sight reading, to spot a respawn wave pushing the counts back up.</summary>
    private int lastSilverReading;

    private int lastBronzeReading;

    /// <summary>When that reading arrived. A reading is only compared against another reading, never against the running count.</summary>
    private DateTime? lastReadingAt;

    protected override void BeforeTick()
    {
        advisor.EnsureLoaded();
        WatchForRespawnWave();
    }

    /// <summary>Confirmed coffers are discounted so a chest you have already laid eyes on wins over an unchecked spot at the same distance.</summary>
    protected override float ScoreFor(CofferSpot spot, float distance)
    {
        return spot.Status == SpotStatus.Present ? distance * config.ConfirmedCofferBias : distance;
    }

    protected override int PlanRevision => HashCode.Combine(advisor.IsReady, config.UseRouteDistances);

    /// <summary>
    ///     Graph-ordered when the zone has a precomputed hunt graph, because real navmesh distances already price the
    ///     descent in; straight lines otherwise, which is what the base class does.
    /// </summary>
    protected override List<uint> BuildPlan(List<CofferSpot> candidates)
    {
        if (!config.UseRouteDistances || !advisor.IsReady)
        {
            return base.BuildPlan(candidates);
        }

        Vector3 from = PlayerPosition;
        uint start = Target != null && candidates.Contains(Target)
            ? Target.Id
            : (PickNext() ?? candidates.MinBy(s => s.DistanceTo(from))!).Id;

        return advisor.Order(start, candidates.Select(s => s.Id));
    }

    protected override string? DescribeOutcome(CofferSpot spot, SpotStatus previous)
    {
        if (previous != SpotStatus.Unknown)
        {
            return null;
        }

        return spot.Status switch
        {
            SpotStatus.Present => "has a coffer",
            SpotStatus.Opened => "was already opened",
            var _ => "is empty"
        };
    }

    #region Counts

    /// <summary>
    ///     Coffers of this tier currently spawned across the instance, as last reported by Treasure Sight. The reading
    ///     only moves when Treasure Sight is recast, so it drifts — see <see cref="CountsAreStale" />.
    /// </summary>
    public int SpawnedCount(TreasureType type)
    {
        return type == TreasureType.Silver ? treasure.SilverChests : treasure.BronzeChests;
    }

    public bool CountsKnown => treasure.CountInitialised;

    /// <summary>
    ///     When the banner last yielded a reading, or null before the first one. Only moves on a Treasure Sight recast,
    ///     which is what lets a consumer compare one reading against the previous one rather than against a running total.
    /// </summary>
    public DateTime? CountsUpdatedAt => treasure.LastCountUpdateUtc == DateTime.MinValue ? null : treasure.LastCountUpdateUtc;

    public bool CountsAreStale
    {
        get
        {
            DateTime? at = CountsUpdatedAt;
            return at == null || DateTime.UtcNow - at.Value > TimeSpan.FromMinutes(config.StaleReadingMinutes);
        }
    }

    /// <summary>Coffers of this tier sitting on a spot we have eyes on. These are banked as soon as you walk over.</summary>
    public int LocatedCount(TreasureType type)
    {
        return tracker.Count(type, SpotStatus.Present);
    }

    public int UncheckedCount(TreasureType type)
    {
        return tracker.Count(type, SpotStatus.Unknown);
    }

    /// <summary>
    ///     Coffers of this tier that are out there but not yet located — the instance's count minus the ones already
    ///     found. This is the number that decides whether there is any point continuing to search: at zero, every
    ///     remaining coffer is already on the map and the unchecked spots are all empty.
    /// </summary>
    public int UnfoundCount(TreasureType type)
    {
        return Math.Max(0, SpawnedCount(type) - LocatedCount(type));
    }

    /// <summary>Chance that any given unchecked spot of this tier is holding a coffer, or null when it cannot be told.</summary>
    public float? OddsPerUncheckedSpot(TreasureType type)
    {
        int remaining = UncheckedCount(type);
        if (!CountsKnown || remaining <= 0)
        {
            return null;
        }

        return Math.Clamp(UnfoundCount(type) / (float)remaining, 0f, 1f);
    }

    /// <summary>
    ///     A respawn wave is invisible except through Treasure Sight: one reading is higher than the one before it.
    ///     <para>
    ///         Both halves of that sentence matter. Comparing each frame's count against the last is not a comparison of
    ///         readings at all — the tracker counts coffers down as they are opened, so the next Treasure Sight cast
    ///         almost always reads higher than the running total, and every recast would wipe the lap's checks.
    ///         Comparing only reading against reading is what makes the increase mean what it says.
    ///     </para>
    ///     <para>
    ///         And only the resolved spots are cleared: a coffer that respawned may have landed on any spot already
    ///         found empty, but the ones still holding a coffer are unaffected and re-walking them would be wasted.
    ///     </para>
    /// </summary>
    private void WatchForRespawnWave()
    {
        DateTime? readingAt = CountsUpdatedAt;
        if (readingAt == null || readingAt == lastReadingAt)
        {
            return;
        }

        int silver = treasure.SilverChests;
        int bronze = treasure.BronzeChests;

        bool first = lastReadingAt == null;
        int previousSilver = lastSilverReading;
        int previousBronze = lastBronzeReading;

        lastReadingAt = readingAt;
        lastSilverReading = silver;
        lastBronzeReading = bronze;

        if (first || !config.ResetChecksOnRespawn)
        {
            return;
        }

        if (silver <= previousSilver && bronze <= previousBronze)
        {
            return;
        }

        int cleared = tracker.ResetResolved();
        if (cleared == 0)
        {
            return;
        }

        Announce($"Treasure Sight now reports {silver} silver and {bronze} bronze, up from {previousSilver} and {previousBronze} — "
                 + $"{cleared} finished spot(s) are worth checking again.");
    }

    #endregion
}
