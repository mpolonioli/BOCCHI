using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Graphics;
using Ocelot.Lifecycle;
using Ocelot.Services.OverlayRenderer;
using Ocelot.Services.PlayerState;
using System.Numerics;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     Draws the spawn points themselves, which the object radars cannot: they can only draw what the client has
///     streamed in, and the whole problem of a manual hunt is knowing where to go before anything is in render range.
/// </summary>
public abstract class GuidedHuntRadar<TSpot>
(
    GuidedHuntService<TSpot> hunt,
    IGuidedHuntConfig config,
    IZoneProvider zones,
    ICondition conditions,
    IOverlayRenderer overlay,
    IPlayer player
) : IOnRender
    where TSpot : GuidedSpot
{
    /// <summary>Beyond this, markers stack up into noise at the horizon. The target is always drawn regardless.</summary>
    private const float MaxMarkerDistance = 250f;

    private static readonly Color TargetColor = new(1f, 0.85f, 0.2f);

    public void Render()
    {
        if (!config.Enabled || !config.DrawSpotMarkers)
        {
            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone() || player.PlayerCharacter == null)
        {
            return;
        }

        if (config.HideMarkersInCombat && conditions[ConditionFlag.InCombat])
        {
            return;
        }

        Vector3 origin = player.Position;

        foreach(TSpot spot in hunt.Tracker.Spots)
        {
            if (!hunt.IsCandidate(spot) || ReferenceEquals(spot, hunt.Target))
            {
                continue;
            }

            if (Vector3.Distance(origin, spot.Position) > MaxMarkerDistance)
            {
                continue;
            }

            Color color = ToColor(spot.GetColor());

            if (spot.Status == SpotStatus.Present)
            {
                // Confirmed find. Filled so it reads as "something is there" at a glance, unlike the hollow unchecked ones.
                overlay.FillCircle(spot.Position, 1.5f, color);
                overlay.StrokeCircle(spot.Position, 3f, color);
                continue;
            }

            overlay.StrokeCircle(spot.Position, 2f, color.WithAlpha(0.35f));
        }

        TSpot? target = hunt.Target;
        if (target == null)
        {
            return;
        }

        overlay.StrokeCircle(target.Position, 4f, TargetColor);
        overlay.StrokeCircle(target.Position, 2f, TargetColor);

        if (config.DrawLineToTarget)
        {
            overlay.StrokeLine(origin, target.Position, TargetColor);
        }
    }

    private static Color ToColor(Vector4 color) => new(color.X, color.Y, color.Z, color.W);
}
