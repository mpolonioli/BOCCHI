using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Services.OverlayRenderer;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     Carrot spawn points in the world. Once the carrot itself has been sighted the hunt narrows to that one spot, so
///     this narrows with it — the other markers come off the screen rather than competing with the answer.
/// </summary>
public class GuidedCarrotRadar
(
    GuidedCarrotService guided,
    GuidedCarrotConfig config,
    IZoneProvider zones,
    ICondition conditions,
    IOverlayRenderer overlay,
    IPlayer player
) : GuidedHuntRadar<CarrotSpot>(guided, config, zones, conditions, overlay, player);
