using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     The hand-run carrot hunt. Same machinery as the treasure hunt — pick a spot, flag it, mark it off on approach —
///     over a search that behaves nothing like it.
///     <para>
///         There is exactly one carrot in the zone at any moment, so the hunt is never about collecting a set: it is
///         about finding one thing that moves. That has three consequences, and they are the whole of this class. Once
///         the carrot is seen, nothing else in the zone is worth walking to. Once it is taken, every other spot goes
///         back to being a candidate, because the replacement was placed with no regard to where anyone has been. And a
///         zone swept clean with no carrot found is not a finished hunt — it is proof the readings went stale behind
///         the player, so the sweep starts again.
///     </para>
/// </summary>
public class GuidedCarrotService
(
    GuidedCarrotTracker tracker,
    GuidedHuntCoordinator coordinator,
    IZoneProvider zones,
    ISubMapResolver subMaps,
    IClientState clientState,
    IObjectTable objects,
    IChatGui chat,
    IPluginLog log,
    GuidedCarrotConfig config
) : GuidedHuntService<CarrotSpot>(tracker, config, coordinator, zones, subMaps, clientState, objects, chat, log)
{
    public GuidedCarrotTracker Carrots => tracker;

    public GuidedCarrotConfig Config => config;

    public override string Label => "carrot hunt";

    protected override string Key => "carrot";

    /// <summary>Sweeps run since the hunt started — one per time the zone was searched without turning up the carrot.</summary>
    public int Sweeps { get; private set; }

    public override void OnStart()
    {
        base.OnStart();
        tracker.OnCarrotTaken += OnCarrotTaken;
        tracker.OnSpotDiscovered += OnSpotDiscovered;
    }

    public override void OnStop()
    {
        base.OnStop();
        tracker.OnCarrotTaken -= OnCarrotTaken;
        tracker.OnSpotDiscovered -= OnSpotDiscovered;
    }

    /// <summary>
    ///     Once the carrot has been seen, it is the only thing in the zone worth walking to — every other spot is
    ///     provably empty, because there is only ever one. Until then, every unchecked spot is a candidate as usual.
    /// </summary>
    public override bool IsCandidate(CarrotSpot spot)
    {
        if (!base.IsCandidate(spot))
        {
            return false;
        }

        CarrotSpot? located = tracker.Located;
        return located == null || ReferenceEquals(spot, located);
    }

    /// <summary>
    ///     Nothing left to check means the carrot was somewhere behind us all along — someone took it and it respawned
    ///     on a spot already crossed off. The only correct response is to forget the sweep and run it again.
    /// </summary>
    protected override void OnNothingLeft()
    {
        // The carrot is located but not a candidate: the player skipped that spot deliberately. Their call, not a stale
        // sweep — clearing the checks would only hand back the same target.
        if (tracker.Located != null)
        {
            return;
        }

        bool anythingToClear = tracker.Spots.Any(spot => spot.IsResolved && !IsSkipped(spot) && !IsAboveMyLevel(spot));
        if (!anythingToClear)
        {
            return;
        }

        tracker.ResetSweep();
        Sweeps++;

        Announce($"Swept every spot without finding the carrot — it moved behind us. Starting sweep {Sweeps + 1}.");
    }

    protected override string? DescribeOutcome(CarrotSpot spot, SpotStatus previous)
    {
        // Only a sighting is worth a line. Empties churn constantly here — every take resets them — so announcing them
        // would bury the one message that matters in the log.
        return spot.Status == SpotStatus.Present ? "has the carrot" : null;
    }

    private void OnCarrotTaken(CarrotSpot spot, bool mine)
    {
        Announce(mine
            ? $"Carrot collected at {spot.Label} — the next one is somewhere else, so every other spot is worth checking again."
            : $"{spot.Label} was emptied by someone else — the carrot has moved, so every other spot is worth checking again.");

        // The spot that just emptied is very likely the current target; re-picking now rather than next frame means the
        // flag has already moved by the time the player looks up from the pickup.
        MaintainTarget();
    }

    private void OnSpotDiscovered(CarrotSpot spot)
    {
        Announce($"Found a carrot at a spawn point not in the zone's table — tracking it as {spot.Label} for this session.");
    }

    public override void ResetChecks()
    {
        base.ResetChecks();
        Sweeps = 0;
    }
}
