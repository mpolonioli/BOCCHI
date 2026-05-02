using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph;

public class TeleportNodeMetadata : INodeMetadata
{
    public uint AetheryteId { get; set; } = 0;

    public float InteractRange { get; set; } = 3f;

    public Vector3 Destination { get; set; } = Vector3.Zero;

    public bool Unlocked { get; set; } = false;
}
