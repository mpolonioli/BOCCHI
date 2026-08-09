using BOCCHI.Common;
using BOCCHI.Treasure.ChainRecipes;
using BOCCHI.Treasure.Guided;
using BOCCHI.Treasure.Services;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Windows;

namespace BOCCHI.Treasure;

public static class IServiceCollectionExtensions
{
    public static void LoadTreasureModule(this IServiceCollection services)
    {
        services.AddSingleton<ITreasureTracker, TreasureTracker>();
        services.AddSingleton<ICarrotTracker, CarrotTracker>();
        services.AddSingleton<ITreasureHunter, TreasureHunterService>();
        services.AddSingleton<FortuneCarrotAssist>();
        services.AddSingleton<ICarrotHunter, CarrotHunterService>();
        services.AddSingleton<NinjaHideAssist>();
        services.AddSingleton<Func<ITreasureHunter>>(sp => () => sp.GetRequiredService<ITreasureHunter>());
        services.AddSingleton<Func<ICarrotHunter>>(sp => () => sp.GetRequiredService<ICarrotHunter>());
        services.AddSingleton<IDynamicRenderer, TreasureRenderer>();
        services.AddSingleton<TreasureRadarDrawer>();
        services.AddSingleton<OpenTreasureCofferChain>();
        services.AddSingleton<HuntTreasureSightChain>();
        services.AddSingleton<PandoraAutoOpenHold>();

        // Guided hunts — the hand-run counterparts. They read the world and place map flags; neither drives the
        // character, so both are deliberately outside Illegal Mode. The coordinator is what keeps them from fighting
        // over the single map flag.
        services.AddSingleton<GuidedHuntCoordinator>();
        services.AddSingleton<GuidedRouteAdvisor>();

        services.AddSingleton<GuidedCofferTracker>();
        services.AddSingleton<GuidedTreasureService>();
        services.AddSingleton<GuidedTreasureRadar>();
        services.AddSingleton<GuidedTreasureTab>();

        services.AddSingleton<GuidedCarrotTracker>();
        services.AddSingleton<GuidedCarrotService>();
        services.AddSingleton<GuidedCarrotRadar>();
        services.AddSingleton<GuidedCarrotTab>();

        services.AddSingleton<IDynamicRenderer, GuidedHuntRenderer>();
        services.AddSingleton<GuidedHuntWindow>();
        services.AddSingleton<IGuidedHuntWindow>(sp => sp.GetRequiredService<GuidedHuntWindow>());
        services.AddSingleton<IWindow>(sp => sp.GetRequiredService<GuidedHuntWindow>());
    }
}
