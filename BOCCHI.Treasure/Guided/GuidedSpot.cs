using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>What a guided hunt knows about one spawn point right now.</summary>
public enum SpotStatus
{
    /// <summary>Never seen from close enough to tell. Still worth walking to.</summary>
    Unknown,

    /// <summary>Something is sitting there to be collected. Go take it.</summary>
    Present,

    /// <summary>Observed from inside the observation range with nothing there.</summary>
    Empty,

    /// <summary>
    ///     Seen already opened. Sticky until the checks are reset, so a coffer that despawns after looting is not
    ///     re-offered. Only coffers reach this state — a carrot is either there or it is not.
    /// </summary>
    Opened
}

/// <summary>
///     One spawn point a guided hunt walks between, plus whatever has been observed about it this cycle. Spawn points
///     are fixed; what changes is whether the thing that spawns there happens to be sitting on one, which is what makes
///     a hunt a search problem rather than a route.
/// </summary>
public abstract class GuidedSpot(uint id, Vector3 position, uint level)
{
    /// <summary>Identifies the spot within its own hunt. Not unique across hunts, and not meant to be.</summary>
    public uint Id { get; } = id;

    public Vector3 Position { get; } = position;

    /// <summary>
    ///     Level of the mobs around this spawn point, from the zone's table. Not a gate on collecting anything — it is
    ///     what tells you whether walking up to it is survivable. 0 when the table has no entry.
    /// </summary>
    public uint Level { get; } = level;

    /// <summary>
    ///     Map this spot's position belongs to, which is not always the zone's own map — North Horn puts spawn points on
    ///     two sub-maps under the open zone. 0 when the zone is not split, or when the split could not be read.
    /// </summary>
    public uint MapId { get; private set; }

    /// <summary>Name of that map, for the cases where "150y east" is a lie because the spot is under the world.</summary>
    public string? Area { get; private set; }

    public SpotStatus Status { get; private set; } = SpotStatus.Unknown;

    public DateTime? ObservedAt { get; private set; }

    /// <summary>True once the spot has been observed at all, whatever the outcome.</summary>
    public bool IsChecked => Status != SpotStatus.Unknown;

    /// <summary>Nothing left to collect here this cycle.</summary>
    public bool IsResolved => Status is SpotStatus.Empty or SpotStatus.Opened;

    /// <summary>Either a confirmed find or an unchecked spot — the two things a hunt still has a reason to visit.</summary>
    public bool IsWorthVisiting => Status is SpotStatus.Unknown or SpotStatus.Present;

    /// <summary>How this spot is named in the table, the chat log and the target line.</summary>
    public abstract string Label { get; }

    public abstract Vector4 GetColor();

    public float DistanceTo(Vector3 from)
    {
        return Vector3.Distance(from, Position);
    }

    /// <summary>
    ///     Folds one observation into the spot's state.
    ///     <para>
    ///         <see cref="SpotStatus.Opened" /> is sticky: the game despawns an opened coffer a few seconds later, and
    ///         without stickiness the spot would immediately read as <see cref="SpotStatus.Empty" /> and be
    ///         indistinguishable from one somebody else had already emptied — which loses the only record that this run
    ///         actually banked it.
    ///     </para>
    /// </summary>
    /// <returns>True when the status actually changed.</returns>
    public bool Observe(SpotStatus observed)
    {
        ObservedAt = DateTime.Now;

        if (Status == SpotStatus.Opened || Status == observed)
        {
            return false;
        }

        Status = observed;
        return true;
    }

    /// <summary>Records which map this spot is on. Pass 0 to clear it, which puts the spot back on a flat zone.</summary>
    public void SetArea(uint mapId, string? area)
    {
        MapId = mapId;
        Area = mapId == 0 ? null : area;
    }

    /// <summary>Forces a status without an observation, for the manual overrides in the table.</summary>
    public void Mark(SpotStatus status)
    {
        Status = status;
        ObservedAt = DateTime.Now;
    }

    public void Reset()
    {
        Status = SpotStatus.Unknown;
        ObservedAt = null;
    }

    /// <summary>Compass bearing from a position, as an eight-point cardinal. North is -Z and east is +X in world space.</summary>
    public string BearingFrom(Vector3 from)
    {
        Vector3 delta = Position - from;
        if (delta.LengthSquared() < 1f)
        {
            return "here";
        }

        float degrees = MathF.Atan2(delta.X, -delta.Z) * (180f / MathF.PI);
        if (degrees < 0)
        {
            degrees += 360f;
        }

        int sector = (int)MathF.Round(degrees / 45f) % 8;

        return sector switch
        {
            0 => "N",
            1 => "NE",
            2 => "E",
            3 => "SE",
            4 => "S",
            5 => "SW",
            6 => "W",
            var _ => "NW"
        };
    }
}
