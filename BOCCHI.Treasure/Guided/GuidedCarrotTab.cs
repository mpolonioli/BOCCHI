using BOCCHI.Common.Config;
using Dalamud.Bindings.ImGui;
using Ocelot.Config;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     The carrot half of the guided window. The table is the same; what it needs said above it is not, because there
///     is no total to count down to — only one carrot, somewhere, and a sweep that starts over whenever it moves.
/// </summary>
public class GuidedCarrotTab(GuidedCarrotService guided, GuidedCarrotConfig config, IConfigSaver saver)
    : GuidedHuntTab<CarrotSpot>(guided, config, saver)
{
    protected override string Id => "GuidedCarrots";

    protected override string EmptyMessage => "This zone has no carrot spawn points in its table.";

    protected override void DrawSession()
    {
        GuidedCarrotTracker carrots = guided.Carrots;
        TimeSpan elapsed = DateTime.Now - carrots.SessionStartedAt;

        ImGui.TextUnformatted(
            $"Checked {carrots.CheckedCount}/{carrots.Spots.Count} · collected {carrots.CarrotsCollected}"
            + $" · lost {carrots.CarrotsLost} · sweep {guided.Sweeps + 1} · {elapsed:hh\\:mm\\:ss}");

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Collected: carrots that vanished while you were standing on them.\n"
                + "Lost: ones somebody else took — which is just as useful, because it tells you the carrot has moved.\n"
                + "A new sweep starts whenever every spot has been checked without finding it.");
        }
    }

    /// <summary>
    ///     The one thing worth knowing: whether the carrot's position is known. Everything else on this tab is in
    ///     service of that answer, so it gets a line of its own in the two states it can be in.
    /// </summary>
    protected override void DrawSummary()
    {
        GuidedCarrotTracker carrots = guided.Carrots;
        CarrotSpot? located = carrots.Located;

        if (located != null)
        {
            Vector3 from = guided.PlayerPosition;
            string where = guided.IsElsewhere(located)
                ? located.Area ?? "another part of the zone"
                : $"{located.DistanceTo(from):f0}y {located.BearingFrom(from)}";

            ImGui.TextColored(GuidedSummary.Good, $"Carrot located — {located.Label}, {where}. Every other spot is empty until it moves.");
        }
        else
        {
            int left = guided.Candidates().Count();
            if (left > 0)
            {
                ImGui.TextColored(
                    GuidedSummary.Muted,
                    $"Carrot not located — {left} spot(s) left this sweep, about {100f / left:f0}% per spot.");
            }
            else
            {
                ImGui.TextColored(GuidedSummary.Warning, "Nothing left to check — the carrot moved behind us, so the sweep starts again.");
            }
        }

        if (carrots.LastTaken is not { } taken)
        {
            return;
        }

        ImGui.TextColored(GuidedSummary.Muted, $"Last taken: {taken.Label}, {GuidedSummary.Age(carrots.LastTakenAt?.ToUniversalTime())} ago.");
    }

    protected override (string Label, Vector4 Color) Status(CarrotSpot spot)
    {
        // "Taken" rather than "Empty" for the spot the carrot was last seen leaving: they are the same fact, but that
        // one is the only spot in the zone the carrot certainly is not on, which is worth calling out.
        if (spot.Status == SpotStatus.Empty && ReferenceEquals(spot, guided.Carrots.LastTaken))
        {
            return ("Taken", GuidedSummary.Muted);
        }

        return spot.Status switch
        {
            SpotStatus.Present => ("Carrot", GuidedSummary.Good),
            SpotStatus.Empty => ("Empty", GuidedSummary.Muted),
            var _ => ("Unchecked", GuidedSummary.Warning)
        };
    }
}
