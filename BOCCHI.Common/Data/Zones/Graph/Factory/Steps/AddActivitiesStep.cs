using Ocelot.Extensions;

namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public class AddActivitiesStep : IGraphBuildStep
{
    public async Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        foreach (var fate in zone.GetNormalFateData())
        {
            graph.AddNode(new Node
            {
                Type = NodeType.NormalFate,
                Position = fate.Position,
                Metadata = new ActivityNodeMetadata { Id = fate.Id, },
            });
        }

        foreach (var fate in zone.GetPotFateData())
        {
            graph.AddNode(new Node
            {
                Type = NodeType.PotFate,
                Position = fate.Position,
                Metadata = new ActivityNodeMetadata { Id = fate.Id, },
            });
        }

        foreach (var criticalEncounter in zone.GetCriticalEncounterData())
        {
            graph.AddNode(new Node
            {
                Type = NodeType.CriticalEncounter,
                Position = criticalEncounter.Position,
                Metadata = new ActivityNodeMetadata { Id = criticalEncounter.Id, },
            });
        }

        var activities = graph.GetActivityNodes().ToList();

        await graph.ConnectToNearestTeleports(activities, config);
        await graph.ConnectToBaseCamp(activities, config);
    }
}
