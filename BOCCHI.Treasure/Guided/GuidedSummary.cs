using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Treasure.Data;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     The readouts that turn raw counts into a decision.
///     <para>
///         Treasure Sight reports how many coffers of each tier are spawned across the instance; the tracker knows which
///         spawn points have been looked at. Subtracting one from the other gives the only number that matters while
///         hunting — how many coffers are still hiding, and across how many spots — and, at zero, the moment to stop
///         searching and just collect what is already on the map.
///     </para>
/// </summary>
public static class GuidedSummary
{
    public static readonly Vector4 Good = new(0.4f, 0.85f, 0.4f, 1f);

    public static readonly Vector4 Muted = new(0.65f, 0.65f, 0.65f, 1f);

    public static readonly Vector4 Warning = new(0.95f, 0.8f, 0.3f, 1f);

    public static string TierLabel(TreasureType type) => type == TreasureType.Silver ? "Silver" : "Bronze";

    private static Vector4 TierColor(TreasureType type) =>
        type == TreasureType.Silver ? TreasureColors.Silver : TreasureColors.Bronze;

    public static void DrawTierLine(GuidedTreasureService guided, TreasureType type)
    {
        ImGui.TextColored(TierColor(type), $"{TierLabel(type)}:");
        ImGui.SameLine();

        if (!guided.CountsKnown)
        {
            ImGui.TextUnformatted($"{guided.UncheckedCount(type)} spot(s) unchecked");
            return;
        }

        int unfound = guided.UnfoundCount(type);
        int located = guided.LocatedCount(type);
        int spots = guided.UncheckedCount(type);

        ImGui.TextUnformatted($"{unfound} still hiding across {spots} spot(s), {located} located");

        if (!guided.CountsAreStale)
        {
            return;
        }

        ImGui.SameLine();
        ImGui.TextColored(Warning, "*");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Treasure Sight reading is {Age(guided.CountsUpdatedAt)} old. Recast it to refresh these counts.");
        }
    }

    /// <summary>
    ///     The two states worth interrupting the player for: nothing left to find, or every remaining spot holding one.
    ///     Both change what to do next, which is why they get a line of their own rather than being left to be inferred
    ///     from the numbers.
    /// </summary>
    public static void DrawCallouts(GuidedTreasureService guided)
    {
        if (!guided.CountsKnown)
        {
            ImGui.TextColored(Warning, "No Treasure Sight reading yet — cast it to see how many coffers are out there.");
            return;
        }

        foreach(TreasureType type in new[] { TreasureType.Silver, TreasureType.Bronze })
        {
            int unfound = guided.UnfoundCount(type);
            int spots = guided.UncheckedCount(type);
            int located = guided.LocatedCount(type);
            string label = TierLabel(type);

            if (unfound == 0 && located > 0 && spots > 0)
            {
                ImGui.TextColored(Good, $"All {label.ToLowerInvariant()} coffers located ({located}) — the rest of the spots are empty.");
                continue;
            }

            if (spots > 0 && unfound >= spots)
            {
                ImGui.TextColored(Good, $"Every remaining {label.ToLowerInvariant()} spot ({spots}) should be holding one.");
                continue;
            }

            float? odds = guided.OddsPerUncheckedSpot(type);
            if (odds != null && spots > 0)
            {
                ImGui.TextColored(Muted, $"{label}: about {odds.Value * 100f:f0}% per spot across {spots} left.");
            }
        }
    }

    public static string Age(DateTime? at)
    {
        if (at == null)
        {
            return "—";
        }

        TimeSpan elapsed = DateTime.UtcNow - at.Value;
        return elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes}m" : $"{(int)elapsed.TotalSeconds}s";
    }

    /// <summary>Splits an aethernet enum name into words for display: <c>SinkingSanctuary</c> → "Sinking Sanctuary".</summary>
    public static string Humanise(Enum value)
    {
        string name = value.ToString();
        return string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) ? $" {c}" : $"{c}"));
    }
}
