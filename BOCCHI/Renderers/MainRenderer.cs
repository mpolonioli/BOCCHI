using BOCCHI.Common;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Services.UI;
using Ocelot.Services.WindowManager;

namespace BOCCHI.Renderers;

public class MainRenderer(IEnumerable<IDynamicRenderer> renderers, ICondition conditions, IUIService ui) : IMainRenderer
{
    private IEnumerable<IDynamicRenderer> orderedRenderers
    {
        get => renderers.Where(r => r.ShouldRender()).OrderBy(r => r.Order);
    }

    public void Render()
    {
        foreach (var renderer in orderedRenderers)
        {
            renderer.Render();
        }

        unsafe
        {

            var dec = DynamicEventContainer.GetInstance();
            if (dec != null)
            {
                ui.LabelledValue("DEC", dec->CurrentEventId);
            }
            else
            {
                ui.LabelledValue("DEC", "NONE");
            }
        }
    }
}
