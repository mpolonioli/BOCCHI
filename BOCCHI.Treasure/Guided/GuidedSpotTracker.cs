using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     Holds a hunt's spawn points and keeps their status current as the player moves past them. What spawns there and
///     where the points come from is the derived tracker's business; the observation rule is shared, because it is the
///     part that has to be right.
/// </summary>
public abstract class GuidedSpotTracker<TSpot>(IObjectTable objects, IZoneProvider zones, ISubMapResolver subMaps, IPluginLog log)
    where TSpot : GuidedSpot
{
    /// <summary>One sighting from the world: something is on the ground here, in this state.</summary>
    protected readonly record struct Sighting(Vector3 Position, SpotStatus Status);

    public List<TSpot> Spots { get; protected set; } = [];

    public DateTime SessionStartedAt { get; protected set; } = DateTime.Now;

    /// <summary>Fired whenever a spot's status changes, with the status it held before.</summary>
    public event Action<TSpot, SpotStatus>? OnStatusChanged;

    /// <summary>Prefix for this hunt's log lines, and the discriminator for its throttle keys.</summary>
    protected abstract string Name { get; }

    /// <summary>
    ///     How far a live object may sit from its spawn point and still count as that spot. Things spawn on the point,
    ///     so this only has to absorb the drift between the recorded position and where the object actually lands.
    /// </summary>
    protected abstract float MatchRadius { get; }

    protected IObjectTable Objects => objects;

    protected IZoneProvider Zones => zones;

    protected IPluginLog Log => log;

    /// <summary>Reads or refreshes the spawn point list. Called every tick, so it has to decide for itself when to work.</summary>
    protected abstract void MaintainSpots();

    /// <summary>Everything of this hunt's kind that the client currently has loaded.</summary>
    protected abstract List<Sighting> ScanWorld();

    /// <summary>
    ///     A sighting that matched no known spawn point, which means the spot table is incomplete. Ignored by default.
    /// </summary>
    protected virtual void OnUnmatchedSighting(Sighting sighting)
    {
    }

    /// <summary>
    ///     Runs after a whole observation pass, with everything that changed in it. Deliberately after rather than
    ///     during: a rule that reacts to one spot by rewriting the others cannot run while the others are still being
    ///     observed.
    /// </summary>
    protected virtual void AfterObserve(IReadOnlyList<(TSpot Spot, SpotStatus Previous)> changes)
    {
    }

    public int Count(SpotStatus status)
    {
        return Spots.Count(s => s.Status == status);
    }

    public int CheckedCount => Spots.Count(s => s.IsChecked);

    public void Tick(float observationRange)
    {
        if (!zones.GetZone().IsOccultCrescentZone() || objects.LocalPlayer is null)
        {
            return;
        }

        MaintainSpots();
        MaintainAreas();
        Observe(observationRange);
    }

    protected bool Throttle(string key, int milliseconds)
    {
        return EzThrottler.Throttle($"BOCCHI.Guided.{Name}.{key}", milliseconds);
    }

    /// <summary>
    ///     Keeps each spot's map current. Done here rather than in the scan because the map ranges and the spawn points
    ///     do not necessarily arrive on the same frame — and because a zone whose split stops being trusted mid-run has
    ///     to put its spots back on one flat map rather than keep answering from a stale reading.
    /// </summary>
    private void MaintainAreas()
    {
        if (Spots.Count == 0 || !Throttle("Areas", 1000))
        {
            return;
        }

        bool split = subMaps.HasSubMaps;

        foreach(TSpot spot in Spots)
        {
            if (!split)
            {
                spot.SetArea(0, null);
                continue;
            }

            if (spot.MapId != 0)
            {
                continue;
            }

            uint mapId = subMaps.MapIdFor(spot.Position);
            spot.SetArea(mapId, subMaps.AreaNameFor(mapId));
        }
    }

    /// <summary>
    ///     Resolves every spawn point the player can currently judge. This is what removes the need to tick spots off by
    ///     hand: passing within the observation range is the check.
    ///     <para>
    ///         The range is deliberately one-sided. Something the client has streamed in is a fact at any distance — the
    ///         object would not be in the table at all if it were not there — so a sighting is recorded however far away
    ///         it happened. Only the opposite conclusion needs the range: "nothing here" means the spot is empty only
    ///         from close enough that an object would certainly have loaded.
    ///     </para>
    /// </summary>
    private void Observe(float range)
    {
        if (Spots.Count == 0 || objects.LocalPlayer is not { } localPlayer || !Throttle("Observe", 250))
        {
            return;
        }

        List<Sighting> sightings = ScanWorld();
        HashSet<int> matched = [];
        Vector3 player = localPlayer.Position;
        List<(TSpot Spot, SpotStatus Previous)> changes = [];

        foreach(TSpot spot in Spots)
        {
            int index = NearestSighting(sightings, spot);
            if (index >= 0)
            {
                matched.Add(index);
            }

            // A sighting stands on its own; an absence only counts from inside the range.
            if (index < 0 && Vector3.Distance(player, spot.Position) > range)
            {
                continue;
            }

            SpotStatus observed = index < 0 ? SpotStatus.Empty : sightings[index].Status;
            SpotStatus previous = spot.Status;

            if (!spot.Observe(observed))
            {
                continue;
            }

            // Every transition, so a spot that reads wrong can be traced to the distance it was judged from rather than
            // guessed at from the table.
            log.Debug(
                "[Guided {Name}] {Label}: {Previous} -> {Observed} at {Distance:f0}y (range {Range:f0}y)",
                Name,
                spot.Label,
                previous,
                observed,
                Vector3.Distance(player, spot.Position),
                range);

            changes.Add((spot, previous));
        }

        for(int index = 0; index < sightings.Count; index++)
        {
            if (!matched.Contains(index))
            {
                OnUnmatchedSighting(sightings[index]);
            }
        }

        AfterObserve(changes);

        foreach((TSpot spot, SpotStatus previous) in changes)
        {
            OnStatusChanged?.Invoke(spot, previous);
        }
    }

    /// <summary>Index of the sighting closest to this spot within the match radius, or -1 when nothing is on it.</summary>
    private int NearestSighting(List<Sighting> sightings, TSpot spot)
    {
        int best = -1;
        float bestDistance = MatchRadius;

        for(int index = 0; index < sightings.Count; index++)
        {
            float distance = Vector3.Distance(sightings[index].Position, spot.Position);
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = index;
        }

        return best;
    }

    /// <summary>Puts every spot back to unchecked and starts the session over.</summary>
    public virtual void ResetChecks()
    {
        foreach(TSpot spot in Spots)
        {
            spot.Reset();
        }

        SessionStartedAt = DateTime.Now;
    }

    /// <summary>
    ///     Puts the finished spots — emptied or opened — back to unchecked, leaving confirmed finds and the session
    ///     counters alone. This is the respawn response: what a wave invalidates is the knowledge that a spot was empty.
    /// </summary>
    /// <returns>How many spots were cleared.</returns>
    public int ResetResolved()
    {
        int cleared = 0;

        foreach(TSpot spot in Spots)
        {
            if (!spot.IsResolved)
            {
                continue;
            }

            spot.Reset();
            cleared++;
        }

        return cleared;
    }
}
