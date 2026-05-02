
using System.Numerics;
using BOCCHI.Common.Data.Paths;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class DirectWalkCalculator : IGraphCandidateCalculator
{
    private readonly static float MaxEuclideanDistance2D = 512f;

    public string Key()
    {
        return "DirectWalk";
    }

    public async Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        var euclideanDistance2D = start.Distance2D(goal.Position);
        if (euclideanDistance2D > MaxEuclideanDistance2D)
        {
            return null;
        }

        var path = await pathfinder.Pathfind(new PathfinderConfig(goal.Position.GetApproachPosition(start))
        {
            From = start,
            AllowFlying = false,
        });

        return new(path.Distance, [PathStep.Pathfind(goal.Position)]);
    }
}
