using BOCCHI.Common.Data.Zones.Graph.Factory.Steps;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Graph.Factory;

public interface IGraphFactory
{
    Task<ZoneGraph> BuildAsync(GraphConfig config, IZone zone);
}
