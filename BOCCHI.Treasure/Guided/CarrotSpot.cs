using BOCCHI.Treasure.Data;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     One carrot spawn point, as tracked by the guided carrot hunt.
///     <para>
///         Unlike a coffer spawn point, a carrot spot is never "the one that has a carrot" for long: the zone holds
///         exactly one carrot at a time, so at most one of these is <see cref="SpotStatus.Present" /> and the moment it
///         is taken the carrot is somewhere else entirely.
///     </para>
/// </summary>
public class CarrotSpot(uint id, Vector3 position, uint level, bool authored = true) : GuidedSpot(id, position, level)
{
    /// <summary>
    ///     False for a spot learned from a carrot seen away from every point in the zone's table. Those are real spawn
    ///     points the table is missing, but they only last the session, so the table is where they belong long-term.
    /// </summary>
    public bool IsAuthored { get; } = authored;

    public override string Label => IsAuthored ? $"Carrot #{Id}" : $"Carrot #{Id} (new)";

    public override Vector4 GetColor() => Carrot.Color;
}
