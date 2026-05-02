namespace BOCCHI.Common.Data.Zones.Graph;

public class Edge
{
    public Guid From { get; set; }

    public EdgeType Type { get; set; }

    public Guid To { get; set; }

    public float Cost { get; set; }
}

