namespace BOCCHI.Common.Data.Zones.Graph;

public class ActivityNodeMetadata : INodeMetadata
{
    public bool Available { get; set; } = false;

    public int Id { get; set; }

    // public string? FateType { get; set; } Pot|Normal?
}
