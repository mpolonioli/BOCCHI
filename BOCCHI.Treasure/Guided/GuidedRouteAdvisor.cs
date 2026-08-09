using BOCCHI.Common.Data.Zones;
using BOCCHI.Treasure.Hunt;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Text.Json;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     Reads the same precomputed graph the automated hunt routes over, and answers the two questions a player doing the
///     running themselves actually has: what order should I take these in, and is there a shard closer to this coffer
///     than I am.
///     <para>
///         Straight-line distance is a poor guide in the Occult Crescent — cliffs, water and the aethernet make the
///         nearest coffer on the map regularly the slowest one to reach. The graph holds real navmesh distances, so the
///         suggested order is the one a route would actually take.
///     </para>
///     <para>
///         The graph has no player-to-node edges, only node-to-node and shard-to-node, so the first hop is still chosen
///         by straight-line distance. Everything after it is graph-ordered.
///     </para>
/// </summary>
public class GuidedRouteAdvisor(IDalamudPluginInterface plugin, IClientState clientState, IZoneProvider zones, IPluginLog log)
{
    private const string Filename = "precomputed_treasure_hunt_data.json";

    /// <summary>Flattened <c>NodeToNodeDistances</c>. The shipped form is a list per node, which is too slow to scan per frame.</summary>
    private Dictionary<uint, Dictionary<uint, float>> nodeToNode = new();

    /// <summary>Cheapest shard for each node, as (shard, walking distance from where that shard drops you).</summary>
    private Dictionary<uint, (HuntAethernet Aethernet, float Distance)> bestShard = new();

    private uint loadedTerritory = uint.MaxValue;

    private bool loading;

    public bool IsReady { get; private set; }

    /// <summary>True when the zone simply has no graph on disk, which is a "sort by distance instead" state, not an error.</summary>
    public bool IsMissing { get; private set; }

    /// <summary>Loads the graph for the current zone if it is not already loaded. Cheap to call every tick.</summary>
    public void EnsureLoaded()
    {
        uint territory = clientState.TerritoryType;
        if (loading || territory == loadedTerritory || !zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        loadedTerritory = territory;
        IsReady = false;
        IsMissing = false;
        nodeToNode = new();
        bestShard = new();

        Load(zones.GetZone().ZoneId);
    }

    private async void Load(ZoneId zoneId)
    {
        loading = true;

        try
        {
            string file = HuntRoutePlanner.GetDataFile(plugin, zoneId, Filename);
            if (!File.Exists(file))
            {
                IsMissing = true;
                log.Information("[Guided treasure] No precomputed graph at {File}; falling back to straight-line ordering.", file);
                return;
            }

            string json = await File.ReadAllTextAsync(file);
            HuntNodeDataSchema? data = JsonSerializer.Deserialize<HuntNodeDataSchema>(json);
            if (data == null)
            {
                IsMissing = true;
                return;
            }

            Dictionary<uint, Dictionary<uint, float>> edges = new();
            foreach((uint from, List<HuntToNode> list) in data.NodeToNodeDistances)
            {
                Dictionary<uint, float> to = new();
                foreach(HuntToNode edge in list)
                {
                    to[edge.Id] = edge.Distance;
                }

                edges[from] = to;
            }

            Dictionary<uint, (HuntAethernet, float)> shards = new();
            foreach((HuntAethernet aethernet, List<HuntToNode> list) in data.AethernetToNodeDistances)
            {
                foreach(HuntToNode edge in list)
                {
                    if (!shards.TryGetValue(edge.Id, out (HuntAethernet, float) current) || edge.Distance < current.Item2)
                    {
                        shards[edge.Id] = (aethernet, edge.Distance);
                    }
                }
            }

            nodeToNode = edges;
            bestShard = shards;
            IsReady = edges.Count > 0;
        }
        catch (Exception ex)
        {
            log.Error("[Guided treasure] Could not read the precomputed graph: {Message}", ex.Message);
            IsMissing = true;
        }
        finally
        {
            loading = false;
        }
    }

    public float? Distance(uint from, uint to)
    {
        if (nodeToNode.TryGetValue(from, out Dictionary<uint, float>? edges) && edges.TryGetValue(to, out float distance))
        {
            return distance;
        }

        return null;
    }

    /// <summary>
    ///     The shard that leaves the shortest walk to this coffer, and how long that walk is. Comparing it against the
    ///     player's own distance is what turns "that coffer is 400y away" into "teleport to Stonemarsh and it is 40y".
    /// </summary>
    public (HuntAethernet Aethernet, float Distance)? BestShardFor(uint node)
    {
        return bestShard.TryGetValue(node, out (HuntAethernet Aethernet, float Distance) shard) ? shard : null;
    }

    /// <summary>
    ///     Greedy nearest-neighbour order over the graph, starting at <paramref name="start" />. Nearest-neighbour rather
    ///     than the hunt's nearest-insertion because this order is advisory — the player can and will deviate from it, so
    ///     recomputing a cheap order from wherever they end up beats holding on to an optimal one they already left.
    /// </summary>
    public List<uint> Order(uint start, IEnumerable<uint> nodes)
    {
        HashSet<uint> remaining = nodes.Where(n => n != start).ToHashSet();
        List<uint> order = [start];
        uint current = start;

        while(remaining.Count > 0)
        {
            uint? next = null;
            float bestDistance = float.MaxValue;

            foreach(uint candidate in remaining)
            {
                float? distance = Distance(current, candidate);
                if (distance == null || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance.Value;
                next = candidate;
            }

            // Nodes the graph does not know about — a spawn point added by a patch after the graph was generated —
            // keep their spots in the plan rather than dropping them off the end of the list silently.
            next ??= remaining.First();

            order.Add(next.Value);
            remaining.Remove(next.Value);
            current = next.Value;
        }

        return order;
    }
}
