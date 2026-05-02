using System.Numerics;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Ocelot.Graphics;
using Ocelot.Lifecycle;
using Ocelot.Pictomancy.Services;
using Ocelot.Services.OverlayRenderer;

namespace BOCCHI.Services;

public class DrawCEs(IOverlayRenderer overlay, ICriticalEncounterRepository ces, IZoneProvider zones) : IOnRender
{
    private readonly static List<List<Vector3>> Lines = [[]];
        //     new Vector3(-173.02f, 8.19f, -611.14f),
        //     new Vector3(-172.25f, 7.50f, -609.50f),
        //     new Vector3(-48.10f, 111.76f, -320.00f),
        // ],
        // [
        //     new Vector3(-358.14f, 101.98f, -120.96f),
        //     new Vector3(-356.75f, 101.25f, -122.50f),
        //     new Vector3(-48.10f, 111.76f, -320.00f),
        // ],
        // [
        //     new Vector3(-48.10f, 111.76f, -320.00f),
        //     new Vector3(5.23f, 106.84f, -390.92f),
        //     new Vector3(16.14f, 25.50f, -437.46f),
        //     new Vector3(0.00f, 24.87f, -467.23f),
        //     new Vector3(-13.75f, 25.50f, -470.00f),
        //     new Vector3(-21.50f, 26.75f, -471.50f),
        //     new Vector3(-25.25f, 27.25f, -472.75f),
        //     new Vector3(-71.00f, 23.00f, -502.00f),
        //     new Vector3(-97.32f, 21.51f, -512.00f),
        //     new Vector3(-174.90f, 6.72f, -608.88f),
        //     new Vector3(-173.02f, 8.19f, -611.14f),
        // ],
        // [
        //     new Vector3(-48.10f, 111.76f, -320.00f),
        //     new Vector3(-128.00f, 110.81f, -278.49f),
        //     new Vector3(-187.51f, 109.66f, -256.00f),
        //     new Vector3(-227.00f, 101.25f, -228.50f),
        //     new Vector3(-256.00f, 97.75f, -211.75f),
        //     new Vector3(-294.00f, 98.50f, -183.50f),
        //     new Vector3(-355.50f, 100.00f, -120.96f),
        //     new Vector3(-358.14f, 101.98f, -120.96f),
        // ]];

    public static void AddLine(List<Vector3> line)
    {
        Lines.Add(line);
    }


    public void Render()
    {
        foreach (var ce in ces.SnapshotWithoutForkedTower())
        {
            var radius = ce.Radius;

            overlay.StrokeCircle(ce.Position, radius, Color.Green);
            overlay.StrokeCircle(ce.Position,radius - 2f, new Color(1f, 1f, 0f));
            overlay.StrokeCircle(ce.Position,radius - 7f, Color.Red);
        }

        foreach (var crystal in zones.GetZone().GetNearbyKnowledgeCrystals())
        {
            overlay.StrokeCircle(crystal.Position, 5f, new Color(1f, 0f, 1f));
        }

        foreach (var points in GraphConfig.Lines)
        {
            for (var i = 0; i < points.Count - 1; i++)
            {
                var start = points[i];
                var end   = points[i + 1];

                overlay.StrokeLine(start, end, new Color(1f, 0f, 0f));
            }
        }


    }
}
