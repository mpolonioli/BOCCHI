using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>
///     Settings for the hand-run treasure hunt. Nothing here drives the game — the guided hunt only reads the world and
///     places map flags — so none of it sits behind Illegal Mode.
/// </summary>
[Serializable]
[ConfigGroup("treasure", GroupOrder = 20, Order = 10)]
public class GuidedTreasureConfig : IAutoConfig, IGuidedHuntConfig
{
    [Checkbox]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     How close the player has to get before a spawn point with nothing on it is judged empty. It does not gate the
    ///     other direction: a coffer the client has streamed in is recorded from wherever it was seen. The client only
    ///     streams in coffers within a certain distance, so setting this too high marks spots empty that simply had not
    ///     loaded yet; the automated hunt uses 75y for the same decision, which is the safe ceiling.
    /// </summary>
    [FloatRange(10f, 100f)]
    public float ObservationRange { get; set; } = 60f;

    /// <summary>
    ///     Moves the map flag on to the next spot the moment the current one resolves, so the flag is always something
    ///     to run at and the hunt never needs the window opened mid-run.
    /// </summary>
    [Checkbox]
    public bool AutoFlagNextTarget { get; set; } = true;

    /// <summary>
    ///     Multiplier applied to the distance of spots holding a confirmed coffer when picking the next target. Below 1
    ///     it biases towards banking coffers you have already found over exploring new spots; 1 treats both alike.
    /// </summary>
    [FloatRange(0.1f, 1f)]
    public float ConfirmedCofferBias { get; set; } = 0.5f;

    /// <summary>Orders the plan by real navmesh distances from the precomputed graph instead of straight lines.</summary>
    [Checkbox]
    public bool UseRouteDistances { get; set; } = true;

    [Checkbox]
    public bool DrawSpotMarkers { get; set; } = true;

    [Checkbox]
    public bool DrawLineToTarget { get; set; } = true;

    [Checkbox]
    public bool HideMarkersInCombat { get; set; } = true;

    /// <summary>
    ///     Keeps the table to what is left to do. Off by default: a spot that resolves the moment you walk into range
    ///     would otherwise vanish from the table rather than showing its outcome, which reads as the check never having
    ///     happened.
    /// </summary>
    [Checkbox]
    public bool HideResolvedSpots { get; set; } = false;

    /// <summary>
    ///     Drops spots ringed by mobs at or above your Knowledge Level from the table and from target selection. Those
    ///     are the ones that will aggro on the way in, so on a low-level character they are the spots to leave alone.
    /// </summary>
    [Checkbox]
    public bool HideSpotsAboveMyLevel { get; set; } = false;

    /// <summary>
    ///     Puts each spot's outcome in the chat log as it resolves, so a lap can be run with the window closed and the
    ///     game full-screen.
    /// </summary>
    [Checkbox]
    public bool AnnounceInChat { get; set; } = false;

    /// <summary>
    ///     Clears the finished checks when Treasure Sight reports more coffers than last time, which is the only visible
    ///     sign of a respawn wave. Without it the spots you emptied stay struck off and the hunt reports nothing left.
    /// </summary>
    [Checkbox]
    public bool ResetChecksOnRespawn { get; set; } = true;

    /// <summary>How old a Treasure Sight reading may get before the counts derived from it are flagged as stale.</summary>
    [IntRange(1, 30)]
    public int StaleReadingMinutes { get; set; } = 5;
}
