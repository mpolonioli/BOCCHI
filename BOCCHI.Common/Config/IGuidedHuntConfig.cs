namespace BOCCHI.Common.Config;

/// <summary>
///     The settings every guided hunt has, whatever it is hunting. The two hunts keep separate config objects — their
///     defaults and tooltips differ, and one has settings the other has no use for — but the machinery they share reads
///     them through this.
/// </summary>
public interface IGuidedHuntConfig
{
    bool Enabled { get; set; }

    /// <summary>How close the player has to get before a spawn point with nothing on it is judged empty.</summary>
    float ObservationRange { get; set; }

    bool AutoFlagNextTarget { get; set; }

    bool DrawSpotMarkers { get; set; }

    bool DrawLineToTarget { get; set; }

    bool HideMarkersInCombat { get; set; }

    bool HideResolvedSpots { get; set; }

    bool HideSpotsAboveMyLevel { get; set; }

    bool AnnounceInChat { get; set; }
}
