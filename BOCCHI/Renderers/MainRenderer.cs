using BOCCHI.Common;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Graphics;
using Ocelot.Services.UI;
using Ocelot.Services.WindowManager;

namespace BOCCHI.Renderers;

public class MainRenderer(IEnumerable<IDynamicRenderer> renderers, IZoneProvider zones, IUIService ui) : IMainRenderer
{
    private IEnumerable<IDynamicRenderer> orderedRenderers
    {
        get => renderers.Where(r => r.ShouldRender()).OrderBy(r => r.Order);
    }

    public void Render()
    {
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            // TODO: Translate
            ui.Text("Not in a supported Occult Crescent Zone.", Color.Red);
            return;
        }


        foreach (var renderer in orderedRenderers)
        {
            renderer.Render();
        }
    }
}
