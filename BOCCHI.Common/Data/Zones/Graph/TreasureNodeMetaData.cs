namespace BOCCHI.Common.Data.Zones.Graph;

public enum TreasureType
{
    Silver,
    Bronze,
}


public class TreasureNodeMetaData : INodeMetadata
{
    public TreasureType Type { get; set; }

    public int Level { get; set; }
}
