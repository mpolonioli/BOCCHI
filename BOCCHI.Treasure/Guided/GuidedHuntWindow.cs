using BOCCHI.Common.Data.Zones;
using Dalamud.Bindings.ImGui;
using Ocelot.Windows;

namespace BOCCHI.Treasure.Guided;

public interface IGuidedHuntWindow : IWindow;

/// <summary>
///     The guided hunts' working surface, one tab each. Kept out of the main window because the tables want the height,
///     and because this is the one window worth leaving open while running a lap.
///     <para>
///         Both hunts live here rather than in separate windows because they are the same activity in practice — you
///         run a lap of the zone picking things up — and because only one of them can steer the map flag at a time, so
///         putting them side by side is what makes that trade visible rather than mysterious.
///     </para>
/// </summary>
public sealed class GuidedHuntWindow
(
    GuidedTreasureTab coffers,
    GuidedCarrotTab carrots,
    IZoneProvider zones
) : OcelotWindow("Guided Hunt##BOCCHI"), IGuidedHuntWindow
{
    protected override void Render()
    {
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            ImGui.TextUnformatted("Not in a supported Occult Crescent zone.");
            return;
        }

        if (!ImGui.BeginTabBar("GuidedHuntTabs##BOCCHI"))
        {
            return;
        }

        if (ImGui.BeginTabItem("Coffers##GuidedHunt"))
        {
            coffers.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Carrots##GuidedHunt"))
        {
            carrots.Draw();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
}
