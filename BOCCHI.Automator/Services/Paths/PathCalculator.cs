using System.Numerics;
using BOCCHI.Automator.Data.Goals;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Zones.Graph.Traversal;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Automator.Services.Paths;

public class PathCalculator(
    ICriticalEncounterRepository criticalEncounterRepository,
    IFateRepository fateRepository,
    IPathfinder pathfinder,
    IObjectTable objects,
    IZoneProvider zones,
    ILogger<PathCalculator> logger
) : IPathCalculator
{
    public async Task<Queue<IPathStep>> Calculate(IGoal goal)
    {
        logger.Info("Starting PathCalculator");
        if (objects.LocalPlayer is not { } player)
        {
            logger.Warn("No Player");
            return [];
        }

        var zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            logger.Warn("In wrong zone");
            return [];
        }


        logger.Info("Getting Graph");
        var graph = await zone.GetGraph();
        logger.Info("Got Graph");

        logger.Info("Nodes: " + graph.Nodes.Count);
        logger.Info("Edges: " + graph.Edges.Count);

        logger.Info("Getting goal node.");

        Node goalNode;
        try
        {
            goalNode = GetGoalNode(goal, graph);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            logger.Error(ex.Message);
            return [];
        }

        logger.Info("Got goal node.");
        var destination = goalNode.Position;

        if (player.Position.Distance2D(destination) <= 20f)
        {
            logger.Warn("Too close to destination.");
            return [];
        }

        logger.Info("Creating traverser");
        var traverser = new GraphTraverser(graph, pathfinder, logger);
        traverser.AddCalculator(new DirectWalkCalculator());
        traverser.AddCalculator(new WalkTeleportWalkCalculator());
        traverser.AddCalculator(new ReturnWalkCalculator());
        traverser.AddCalculator(new ReturnTeleportWalkCalculator());

        logger.Info("running pathfind");
        var steps = await traverser.FindPath(player.Position, goalNode);

        logger.Info("Done with PathCalculator");
        return new Queue<IPathStep>(steps);


        // const float teleportCost = 10f;
        // const float returnCost = 60f + 20f + 10f; // Return time + walk to aetheryte + teleport
        //
        // var destination = GetGoalDestination(goal);
        // if (float.IsNaN(destination.X) || float.IsNaN(destination.Y) || float.IsNaN(destination.Z))
        // {
        //     return [];
        // }
        //
        // if (player.Position.Distance2D(destination) <= 20f)
        // {
        //     return [];
        // }
        //
        // var zone = zones.GetZone();
        // if (!zone.IsOccultCrescentZone())
        // {
        //     return [];
        // }
        //
        // async Task<float> Distance(Vector3 from, Vector3 to)
        // {
        //     var path = await pathfinder.Pathfind(new PathfinderConfig(to) { From = from });
        //     return path.Distance;
        // }
        //
        // var walkDirect = await Distance(player.Position, destination);
        //
        // var allShards = zone.GetAetherytes().ToList();
        // if (allShards.Count == 0)
        // {
        //     logger.Warn("No aethernet shards found");
        //     return new Queue<IPathStep>([PathStep.Pathfind(destination)]);
        // }
        //
        // var candidateOriginShards = allShards
        //     .OrderBy(s => s.Position.Distance(player.Position))
        //     .Take(2)
        //     .ToList();
        //
        // AethernetData? bestOriginShard = null;
        // var walkToOriginShard = float.PositiveInfinity;
        //
        // foreach (var shard in candidateOriginShards)
        // {
        //     var d = await Distance(player.Position, shard.Destination);
        //     if (d < walkToOriginShard)
        //     {
        //         walkToOriginShard = d;
        //         bestOriginShard = shard;
        //     }
        // }
        //
        // var candidateDestShards = allShards
        //     .OrderBy(s => s.Position.Distance(destination))
        //     .Take(2)
        //     .ToList();
        //
        // AethernetData? bestDestShard = null;
        // var walkFromDestShardToDestination = float.PositiveInfinity;
        //
        // foreach (var shard in candidateDestShards)
        // {
        //     var d = await Distance(shard.Destination, destination);
        //     if (d < walkFromDestShardToDestination)
        //     {
        //         walkFromDestShardToDestination = d;
        //         bestDestShard = shard;
        //     }
        // }
        //
        // if (bestDestShard == null)
        // {
        //     logger.Warn("Unable to choose a destination shard; walking instead.");
        //     return new Queue<IPathStep>([PathStep.Pathfind(destination)]);
        // }
        //
        // var baseCamp = zone.GetAetherytePosition();
        // var walkFromBaseCamp = await Distance(baseCamp, destination);
        //
        // var costWalk = walkDirect;
        //
        // var costWalkTeleport = float.PositiveInfinity;
        // if (bestOriginShard != null && walkToOriginShard < returnCost)
        // {
        //     costWalkTeleport = walkToOriginShard + teleportCost + walkFromDestShardToDestination;
        // }
        //
        // var costReturnWalk = returnCost + walkFromBaseCamp;
        // var costReturnTeleport = returnCost + teleportCost + walkFromDestShardToDestination;
        //
        // var best = new[]
        // {
        //     (Kind: "Walk", Cost: costWalk),
        //     (Kind: "WalkTeleport", Cost: costWalkTeleport),
        //     (Kind: "ReturnWalk", Cost: costReturnWalk),
        //     (Kind: "ReturnTeleport", Cost: costReturnTeleport),
        // }.OrderBy(x => x.Cost).First();
        //
        // logger.Info(
        //     $"Path choice: {best.Kind} | " +
        //     $"Walk={costWalk:0.0}, Walk+TP={costWalkTeleport:0.0}, Return+Walk={costReturnWalk:0.0}, Return+TP={costReturnTeleport:0.0}"
        // );
        //
        // return best.Kind switch
        // {
        //     "Walk" => new Queue<IPathStep>([
        //         PathStep.Pathfind(destination),
        //     ]),
        //
        //     "WalkTeleport" => new Queue<IPathStep>([
        //         PathStep.Pathfind(bestOriginShard!.Destination),
        //         PathStep.Teleport(bestDestShard.Id),
        //         PathStep.Pathfind(destination),
        //     ]),
        //
        //     "ReturnWalk" => new Queue<IPathStep>([
        //         PathStep.Return(),
        //         PathStep.Pathfind(destination),
        //     ]),
        //
        //     "ReturnTeleport" => new Queue<IPathStep>([
        //         PathStep.Return(),
        //         PathStep.Pathfind(zone.GetAetherytePosition().GetApproachPosition(zone.GetStartingPosition(), 2.5f)),
        //         PathStep.Teleport(bestDestShard.Id),
        //         PathStep.Pathfind(destination),
        //     ]),
        //
        //     _ => new Queue<IPathStep>([
        //         PathStep.Pathfind(destination),
        //     ]),
        // };
    }


    private Node GetGoalNode(IGoal goal, ZoneGraph graph)
    {
        return goal.GoalType switch
        {
            CriticalEncounterGoal(var id) => GetActivityNode(id.Value, graph, NodeType.CriticalEncounter),
            FateGoal(var id) => GetActivityNode(id.Value, graph, NodeType.NormalFate, NodeType.PotFate),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private Node GetActivityNode(int id, ZoneGraph graph, params NodeType[] types)
    {
        var nodes = graph.GetNodesByTypes(types).Where(n =>
        {
            if (n.Metadata is not ActivityNodeMetadata meta)
            {
                return false;
            }

            return meta.Id == id;
        }).ToList();

        return nodes.Count == 0 ? throw new Exception("No nodes for Activity") : nodes.First();
    }
}
