using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     Holds the zone's carrot spawn points and keeps them honest against the one rule that governs carrots: the zone
///     has exactly one, always, and the instant it is taken another appears somewhere else.
///     <para>
///         That rule is what makes this hunt different from the treasure one. A coffer spot found empty stays empty
///         until a respawn wave, so knowledge accumulates over a lap. Carrot knowledge does not: the moment the carrot
///         moves, every "empty" reading in the zone is worthless except for the spot it just left, and holding on to
///         them would have the hunt confidently walking past the one place it could now be.
///     </para>
/// </summary>
public class GuidedCarrotTracker(
    IClientState clientState,
    IObjectTable objects,
    IZoneProvider zones,
    ISubMapResolver subMaps,
    IPluginLog log,
    GuidedCarrotConfig config
) : GuidedSpotTracker<CarrotSpot>(objects, zones, subMaps, log)
{
    protected override string Name => "carrot";

    /// <summary>
    ///     Wider than the coffer hunt's. Coffer points come from the live layout and are exact; carrot points are an
    ///     authored table of hand-collected coordinates, so a carrot can sit a few yalms off the recorded position.
    /// </summary>
    protected override float MatchRadius => 12f;

    /// <summary>How close the player has to have been to claim a carrot as theirs rather than somebody else's.</summary>
    private const float CollectRadius = 8f;

    /// <summary>Ids from here up are learned spots, kept clear of the authored table's own numbering.</summary>
    private const uint DiscoveredIdBase = 1000;

    private uint scannedTerritory = uint.MaxValue;

    /// <summary>The spot holding the carrot right now, as far as anything has been seen. Null while it is unlocated.</summary>
    public CarrotSpot? Located => Spots.FirstOrDefault(s => s.Status == SpotStatus.Present);

    /// <summary>Where the carrot was last seen to go, which is the one spot it is known not to be at now.</summary>
    public CarrotSpot? LastTaken { get; private set; }

    public DateTime? LastTakenAt { get; private set; }

    /// <summary>Carrots that vanished while the player was standing on top of them — i.e. ones they took.</summary>
    public int CarrotsCollected { get; private set; }

    /// <summary>Carrots watched go to somebody else. Not a failure: it is the signal that the carrot has moved.</summary>
    public int CarrotsLost { get; private set; }

    /// <summary>Fired when the carrot leaves a spot, whoever took it.</summary>
    public event Action<CarrotSpot, bool>? OnCarrotTaken;

    /// <summary>Fired when a carrot turns up somewhere the zone's table does not have a spawn point.</summary>
    public event Action<CarrotSpot>? OnSpotDiscovered;

    /// <summary>
    ///     The spawn points come from the zone's authored table rather than the layout: carrots are event objects with
    ///     no layout instances to read, so there is nothing to scrape.
    /// </summary>
    protected override void MaintainSpots()
    {
        uint territory = clientState.TerritoryType;
        if (territory == scannedTerritory)
        {
            return;
        }

        // Only reached in a supported zone — the base tracker will not tick outside one — so an empty table means the
        // zone genuinely has no authored carrot spots, and there is nothing to retry for.
        List<CarrotData> carrots = Zones.GetZone().GetCarrotData();
        scannedTerritory = territory;
        Spots = carrots.Select(c => new CarrotSpot((uint)c.Id, c.Position, (uint)Math.Max(0, c.Level))).ToList();

        LastTaken = null;
        LastTakenAt = null;
        CarrotsCollected = 0;
        CarrotsLost = 0;
        SessionStartedAt = DateTime.Now;

        Log.Information("[Guided carrot] {Count} carrot spawn point(s) from the zone table.", Spots.Count);
    }

    protected override List<Sighting> ScanWorld()
    {
        List<Sighting> sightings = [];

        foreach(IGameObject obj in Objects)
        {
            if (obj.ObjectKind != ObjectKind.EventObj || obj.BaseId != OccultObjectType.Carrot)
            {
                continue;
            }

            if (obj is { IsDead: true } || !obj.IsValid())
            {
                continue;
            }

            sightings.Add(new(obj.Position, SpotStatus.Present));
        }

        return sightings;
    }

    /// <summary>
    ///     A carrot standing well away from every known point means the zone's table is short of a spawn point, not
    ///     that the carrot is in an impossible place. It is tracked for the rest of the session so the hunt can use it,
    ///     and logged with its coordinates so the table can be filled in properly later.
    /// </summary>
    protected override void OnUnmatchedSighting(Sighting sighting)
    {
        if (!config.LearnUnknownSpots)
        {
            return;
        }

        uint id = DiscoveredIdBase + (uint)Spots.Count(s => !s.IsAuthored);
        CarrotSpot spot = new(id, sighting.Position, 0, false);
        Spots.Add(spot);

        CarrotSpot? nearest = Spots.Where(s => s.IsAuthored).MinBy(s => s.DistanceTo(sighting.Position));
        Log.Information(
            "[Guided carrot] Carrot seen at ({X:f2}, {Y:f2}, {Z:f2}), {Distance:f0}y from the nearest known spot ({Nearest}) — "
            + "this spawn point is missing from GetCarrotData().",
            sighting.Position.X,
            sighting.Position.Y,
            sighting.Position.Z,
            nearest?.DistanceTo(sighting.Position) ?? 0f,
            nearest?.Label ?? "none");

        OnSpotDiscovered?.Invoke(spot);
    }

    /// <summary>
    ///     Applies the one-carrot rule after each pass.
    ///     <para>
    ///         A spot going from holding the carrot to holding nothing is the only event that matters, and it means the
    ///         same thing whoever caused it: the carrot is now on some other spot, chosen fresh. So the spot it left is
    ///         the one place it certainly is not, and every other spot — including the ones walked past and found empty
    ///         ten seconds ago — is back to being a place worth checking.
    ///     </para>
    /// </summary>
    protected override void AfterObserve(IReadOnlyList<(CarrotSpot Spot, SpotStatus Previous)> changes)
    {
        foreach((CarrotSpot spot, SpotStatus previous) in changes)
        {
            if (previous != SpotStatus.Present || spot.Status != SpotStatus.Empty)
            {
                continue;
            }

            bool mine = Objects.LocalPlayer is { } player && spot.DistanceTo(player.Position) <= CollectRadius;
            if (mine)
            {
                CarrotsCollected++;
            }
            else
            {
                CarrotsLost++;
            }

            LastTaken = spot;
            LastTakenAt = DateTime.Now;

            foreach(CarrotSpot other in Spots)
            {
                if (!ReferenceEquals(other, spot))
                {
                    other.Reset();
                }
            }

            OnCarrotTaken?.Invoke(spot, mine);
            return;
        }
    }

    /// <summary>
    ///     Puts every spot back to unchecked without touching what the session has counted. This is the response to
    ///     having swept the zone without finding anything: the carrot exists, so the readings must have gone stale
    ///     behind us, and the sweep is worth running again.
    /// </summary>
    public void ResetSweep()
    {
        foreach(CarrotSpot spot in Spots)
        {
            spot.Reset();
        }
    }

    public override void ResetChecks()
    {
        base.ResetChecks();
        LastTaken = null;
        LastTakenAt = null;
        CarrotsCollected = 0;
        CarrotsLost = 0;
    }
}
