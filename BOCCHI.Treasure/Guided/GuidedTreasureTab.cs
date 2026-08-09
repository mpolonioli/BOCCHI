using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Treasure.Hunt;
using Dalamud.Bindings.ImGui;
using Ocelot.Config;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>The coffer half of the guided window: the spot table, plus the Treasure Sight arithmetic that drives it.</summary>
public class GuidedTreasureTab(GuidedTreasureService guided, GuidedTreasureConfig config, IConfigSaver saver)
    : GuidedHuntTab<CofferSpot>(guided, config, saver)
{
    protected override string Id => "GuidedCoffers";

    protected override string EmptyMessage => "No coffer spawn points read from this zone's layout yet.";

    protected override string KindLabel => "Tier";

    protected override int KindOf(CofferSpot spot) => (int)spot.Type;

    protected override void DrawExtraControls()
    {
        ImGui.SameLine();
        if (ImGui.Button($"Rescan layout##{Id}Tab"))
        {
            guided.Coffers.Rescan();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Re-read the coffer spawn points from the zone layout, keeping what has been checked.");
        }
    }

    protected override void DrawSession()
    {
        TimeSpan elapsed = DateTime.Now - guided.Coffers.SessionStartedAt;
        ImGui.TextUnformatted(
            $"Checked {guided.Coffers.CheckedCount}/{guided.Coffers.Spots.Count} · opened {guided.Coffers.CoffersOpened} · {elapsed:hh\\:mm\\:ss}");
    }

    protected override void DrawSummary()
    {
        GuidedSummary.DrawTierLine(guided, TreasureType.Silver);
        GuidedSummary.DrawTierLine(guided, TreasureType.Bronze);
        GuidedSummary.DrawCallouts(guided);
    }

    /// <summary>
    ///     Suggests the shard to teleport to when it leaves a shorter walk than the player's own straight-line distance.
    ///     Comes straight out of the precomputed graph, so the numbers are navmesh distances rather than map distances.
    /// </summary>
    protected override void DrawApproachHint(CofferSpot target, Vector3 from, bool elsewhere)
    {
        (HuntAethernet Aethernet, float Distance)? shard = guided.Advisor.BestShardFor(target.Id);
        if (shard == null)
        {
            return;
        }

        // A spot on another map always gets the hint: the comparison below is against a straight line that does not
        // exist, and the shard is the whole answer to how you get there.
        float direct = target.DistanceTo(from);
        if (!elsewhere && direct <= shard.Value.Distance * 1.5f)
        {
            return;
        }

        ImGui.TextColored(
            GuidedSummary.Muted,
            $"Faster from {GuidedSummary.Humanise(shard.Value.Aethernet)} — {shard.Value.Distance:f0}y walk from that shard.");
    }

    protected override (string Label, Vector4 Color) Status(CofferSpot spot)
    {
        return spot.Status switch
        {
            SpotStatus.Present => ("Coffer", GuidedSummary.Good),
            SpotStatus.Empty => ("Empty", GuidedSummary.Muted),
            SpotStatus.Opened => ("Opened", GuidedSummary.Muted),
            var _ => ("Unchecked", GuidedSummary.Warning)
        };
    }
}
