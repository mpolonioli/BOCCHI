using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Services.OverlayRenderer;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Treasure.Guided;

/// <summary>Coffer spawn points in the world, in their tier colours.</summary>
public class GuidedTreasureRadar
(
    GuidedTreasureService guided,
    GuidedTreasureConfig config,
    IZoneProvider zones,
    ICondition conditions,
    IOverlayRenderer overlay,
    IPlayer player
) : GuidedHuntRadar<CofferSpot>(guided, config, zones, conditions, overlay, player);
