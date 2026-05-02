using System.Numerics;
using BOCCHI.Common.Data.Paths;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class ReturnWalkCalculator : IGraphCandidateCalculator
{
    private readonly static float MaxEuclideanDistance2D = 512f;

    public string Key()
    {
        return "ReturnWalk";
    }

    public async Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        var returnNode = graph.GetBaseCampReturnPositionNode();
        if (returnNode == null)
        {
            return null;
        }

        var euclideanDistance2D = start.Distance2D(returnNode.Position);
        if (euclideanDistance2D > MaxEuclideanDistance2D)
        {
            return null;
        }

        var path = await pathfinder.Pathfind(new PathfinderConfig(goal.Position)
        {
            From = returnNode.Position,
            AllowFlying = false,
        });

        return new TraversalCandidate(
            GraphTraverser.ReturnCost + path.Distance,
            [
                PathStep.Return(),
                PathStep.Pathfind(goal.Position)
            ]
        );
    }
}
