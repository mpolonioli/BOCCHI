using BOCCHI.Common.Data.Zones;
using Ocelot.Lifecycle;
using Ocelot.Windows;

namespace BOCCHI.Services;

public class OpenWindows(IMainWindow? main, IConfigWindow? config, IZoneProvider zones) : IOnStart
{
    public void OnStart()
    {
        main?.IsOpen = true;
        config?.IsOpen = true;

        var zone = zones.GetZone();
        zone.GetGraph();
    }
}
