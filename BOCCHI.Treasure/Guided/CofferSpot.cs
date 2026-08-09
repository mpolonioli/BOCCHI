using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Treasure.Data;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     One coffer spawn point from the zone's layout, as tracked by the guided treasure hunt. Its <c>Id</c> is the
///     Treasure sheet RowId, which is also what the node ids in the precomputed hunt graph are keyed by.
/// </summary>
public class CofferSpot(uint id, Vector3 position, TreasureType type, uint level) : GuidedSpot(id, position, level)
{
    public TreasureType Type { get; } = type;

    public override string Label => $"{Type} coffer #{Id}";

    public override Vector4 GetColor()
    {
        return Type switch
        {
            TreasureType.Bronze => TreasureColors.Bronze,
            TreasureType.Silver => TreasureColors.Silver,
            var _ => TreasureColors.Unknown
        };
    }
}
