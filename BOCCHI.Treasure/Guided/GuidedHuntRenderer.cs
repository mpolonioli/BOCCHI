using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Dalamud.Bindings.ImGui;
using Ocelot.Services.UI;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     The main-window block: enough to start either hunt and run a lap from, with the full tables one button away.
///     <para>
///         Only one hunt steers at a time, so this shows the one that is — and falls back to the coffer counts when
///         neither is, because those are worth reading whether or not anything has been started.
///     </para>
/// </summary>
public class GuidedHuntRenderer
(
    GuidedTreasureService treasure,
    GuidedTreasureConfig treasureConfig,
    GuidedCarrotService carrot,
    GuidedCarrotConfig carrotConfig,
    UIConfig uiConfig,
    IZoneProvider zones,
    IGuidedHuntWindow window,
    IUIService ui
) : IDynamicRenderer
{
    public MainWindowSection Section => MainWindowSection.Treasure;

    public uint Order => 10;

    public bool ShouldRender() => uiConfig.ShowTreasureSection && (treasureConfig.Enabled || carrotConfig.Enabled);

    public void Render()
    {
        ImGui.Separator();
        ui.Text("Guided Hunt");

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        DrawButtons();

        if (carrot.IsGuiding)
        {
            DrawCarrots();
            return;
        }

        DrawCoffers();
    }

    private void DrawButtons()
    {
        bool first = true;

        if (treasureConfig.Enabled)
        {
            first = false;
            if (ImGui.Button(treasure.IsGuiding ? "Stop coffers##GuidedPanel" : "Coffers##GuidedPanel"))
            {
                treasure.Toggle();
            }
        }

        if (carrotConfig.Enabled)
        {
            if (!first)
            {
                ImGui.SameLine();
            }

            first = false;
            if (ImGui.Button(carrot.IsGuiding ? "Stop carrots##GuidedPanel" : "Carrots##GuidedPanel"))
            {
                carrot.Toggle();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("One carrot exists in the zone at a time. This points you at the spots it could be on and starts over when it moves.");
            }
        }

        if (!first)
        {
            ImGui.SameLine();
        }

        if (ImGui.Button("Open##GuidedPanel"))
        {
            window.Toggle();
        }
    }

    private void DrawCoffers()
    {
        if (!treasureConfig.Enabled)
        {
            return;
        }

        if (treasure.Tracker.Spots.Count == 0)
        {
            ImGui.TextUnformatted("No coffer spawn points read from this zone's layout yet.");
            return;
        }

        GuidedSummary.DrawTierLine(treasure, TreasureType.Silver);
        GuidedSummary.DrawTierLine(treasure, TreasureType.Bronze);

        DrawTarget(treasure, "Coffers");
    }

    private void DrawCarrots()
    {
        CarrotSpot? located = carrot.Carrots.Located;
        ImGui.TextColored(
            located == null ? GuidedSummary.Muted : GuidedSummary.Good,
            located == null
                ? $"Carrot not located — {carrot.Candidates().Count()} spot(s) left this sweep."
                : "Carrot located.");

        DrawTarget(carrot, "Carrots");
    }

    private void DrawTarget<TSpot>(GuidedHuntService<TSpot> hunt, string id) where TSpot : GuidedSpot
    {
        TSpot? target = hunt.Target;
        if (target == null)
        {
            if (hunt.IsGuiding)
            {
                ImGui.TextColored(GuidedSummary.Warning, "Nothing left worth walking to.");
            }

            return;
        }

        if (ImGui.Button($"Flag##GuidedPanelTarget{id}"))
        {
            hunt.PlaceFlag(target);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Put the map flag on this spot.");
        }

        Vector3 from = hunt.PlayerPosition;

        ImGui.SameLine();
        ImGui.TextColored(target.GetColor(), target.Label);
        ImGui.SameLine();

        if (hunt.IsElsewhere(target))
        {
            ImGui.TextColored(GuidedSummary.Warning, $"— in {target.Area ?? "another part of the zone"}");
            return;
        }

        ImGui.TextUnformatted($"— {target.DistanceTo(from):f0}y {target.BearingFrom(from)}");
    }
}
