using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>
///     Settings for the hand-run carrot hunt. Like the guided treasure hunt it only reads the world and places map
///     flags — it never moves the character — so none of it sits behind Illegal Mode.
/// </summary>
[Serializable]
[ConfigGroup("treasure", GroupOrder = 20, Order = 20)]
public class GuidedCarrotConfig : IAutoConfig, IGuidedHuntConfig
{
    [Checkbox]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     How close the player has to get before a spawn point with no carrot on it is judged empty. A carrot that has
    ///     streamed in is recorded from wherever it was seen; only the "nothing here" conclusion needs the range. Kept
    ///     below the treasure hunt's default because carrots are event objects, which the client streams in later than
    ///     coffers — set this too high and spots get marked empty that simply had not loaded yet.
    /// </summary>
    [FloatRange(10f, 100f)]
    public float ObservationRange { get; set; } = 50f;

    /// <summary>
    ///     Moves the map flag on to the next spot the moment the current one resolves, so the flag is always something
    ///     to run at and the hunt never needs the window opened mid-run.
    /// </summary>
    [Checkbox]
    public bool AutoFlagNextTarget { get; set; } = true;

    /// <summary>
    ///     Tracks carrots seen well away from every point in the zone's table as spawn points of their own, for the rest
    ///     of the session, and writes their coordinates to the log so the table can be corrected. Off means a carrot in
    ///     an unlisted place is simply never hunted.
    /// </summary>
    [Checkbox]
    public bool LearnUnknownSpots { get; set; } = true;

    [Checkbox]
    public bool DrawSpotMarkers { get; set; } = true;

    [Checkbox]
    public bool DrawLineToTarget { get; set; } = true;

    [Checkbox]
    public bool HideMarkersInCombat { get; set; } = true;

    /// <summary>
    ///     Keeps the table to what is left to check this sweep. Off by default: watching a spot resolve as you walk past
    ///     is how you know the automatic check is working.
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
    ///     Puts sightings, pickups and sweep restarts in the chat log, so a lap can be run with the window closed and
    ///     the game full-screen.
    /// </summary>
    [Checkbox]
    public bool AnnounceInChat { get; set; } = false;
}
