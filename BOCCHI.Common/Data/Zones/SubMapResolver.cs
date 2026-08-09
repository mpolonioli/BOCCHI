using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using System.Numerics;
using MapSheet = Lumina.Excel.Sheets.Map;
using TerritoryTypeSheet = Lumina.Excel.Sheets.TerritoryType;

namespace BOCCHI.Common.Data.Zones;

/// <summary>
///     Answers which of a territory's maps a world position belongs to.
///     <para>
///         A territory is not always one map. North Horn's Dark Territory runs two sub-maps under the open zone — North
///         Basin and the Subterrane — and coffers spawn on both. Two things go wrong without knowing which map a
///         position is on: a map flag lands on whichever map the player happens to be looking at, which puts an
///         underground coffer's flag somewhere on the surface map; and straight-line distance says a coffer 150y
///         *below* you is the closest one, when reaching it means running to the entrance and back down.
///     </para>
/// </summary>
public interface ISubMapResolver
{
    /// <summary>
    ///     True when this territory really is split across several maps and the split is being read the way the game
    ///     reads it. False means callers should behave as though the zone were one flat map — which is what BOCCHI did
    ///     before this existed, and the right thing to fall back to.
    /// </summary>
    bool HasSubMaps { get; }

    /// <summary>
    ///     The map this position sits on. Falls back to the territory's own map for positions inside no range, and to
    ///     the map the player is currently looking at when the split cannot be trusted.
    /// </summary>
    uint MapIdFor(Vector3 position);

    /// <summary>Display name of a map — its sub-name where it has one ("The Subterrane"), otherwise its place name.</summary>
    string? AreaNameFor(uint mapId);
}

/// <summary>
///     Reads the territory's <c>MapRange</c> layout instances — the same trigger volumes the game itself switches the
///     displayed map on — and matches positions against them.
///     <para>
///         The volumes are read from the live layout rather than shipped per zone, so a zone needs no extra data, and a
///         patch that moves a sub-area moves this with it.
///     </para>
/// </summary>
public sealed unsafe class SubMapResolver(IClientState clientState, IObjectTable objects, IDataManager data, IPluginLog log) : ISubMapResolver
{
    /// <summary>
    ///     How many times running our answer for the player's own position may disagree with the map the game has
    ///     actually put them on before the whole reading is written off. One disagreement is ordinary — the game's map
    ///     id lags a boundary crossing, and a teleport lands before it catches up. A run of them means the volumes are
    ///     not being read the way the game reads them, and every answer derived from them is suspect.
    /// </summary>
    private const int DisagreementsBeforeDistrust = 3;

    /// <summary>One <c>MapRange</c> trigger volume: the map it puts you on, and the space it covers.</summary>
    private readonly record struct MapRange(uint MapId, Vector3 Center, Quaternion Rotation, Vector3 Extents, ColliderType Shape)
    {
        public float Volume => Extents.X * Extents.Y * Extents.Z;

        public bool Contains(Vector3 position)
        {
            Vector3 local = Vector3.Transform(position - Center, Quaternion.Inverse(Rotation));

            return Shape switch
            {
                ColliderType.Sphere => local.Length() <= Extents.X,
                ColliderType.Cylinder => MathF.Abs(local.Y) <= Extents.Y && new Vector2(local.X, local.Z).Length() <= Extents.X,
                var _ => MathF.Abs(local.X) <= Extents.X && MathF.Abs(local.Y) <= Extents.Y && MathF.Abs(local.Z) <= Extents.Z
            };
        }
    }

    /// <summary>Ranges for the scanned territory, smallest first, so the tightest one containing a point is the first match.</summary>
    private List<MapRange> ranges = [];

    private readonly Dictionary<uint, string?> names = new();

    private uint scannedTerritory = uint.MaxValue;

    /// <summary>The territory's own map — where positions inside no range belong.</summary>
    private uint baseMapId;

    private bool hasSubMaps;

    private bool distrusted;

    private int disagreements;

    public bool HasSubMaps
    {
        get
        {
            Refresh();
            return hasSubMaps && !distrusted;
        }
    }

    public uint MapIdFor(Vector3 position)
    {
        Refresh();

        if (distrusted)
        {
            return clientState.MapId;
        }

        return Resolve(position) ?? baseMapId;
    }

    public string? AreaNameFor(uint mapId)
    {
        if (mapId == 0)
        {
            return null;
        }

        if (names.TryGetValue(mapId, out string? cached))
        {
            return cached;
        }

        string? name = null;
        if (data.GetExcelSheet<MapSheet>().TryGetRow(mapId, out MapSheet map))
        {
            string sub = map.PlaceNameSub.ValueNullable?.Name.ExtractText() ?? string.Empty;
            string place = map.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            name = !string.IsNullOrWhiteSpace(sub) ? sub : !string.IsNullOrWhiteSpace(place) ? place : null;
        }

        names[mapId] = name;
        return name;
    }

    private uint? Resolve(Vector3 position)
    {
        foreach(MapRange range in ranges)
        {
            if (range.Contains(position))
            {
                return range.MapId;
            }
        }

        return null;
    }

    private void Refresh()
    {
        uint territory = clientState.TerritoryType;
        if (territory != scannedTerritory)
        {
            Reset(territory);
        }

        // The layout is not populated the instant the territory id flips, so the first scans after a zone load come up
        // empty and this keeps retrying — the same shape as the coffer scan.
        if (ranges.Count == 0 && EzThrottler.Throttle("BOCCHI.SubMap.Scan", 1000))
        {
            Scan();
        }

        Verify();
    }

    private void Reset(uint territory)
    {
        scannedTerritory = territory;
        ranges = [];
        hasSubMaps = false;
        distrusted = false;
        disagreements = 0;
        baseMapId = data.GetExcelSheet<TerritoryTypeSheet>().TryGetRow(territory, out TerritoryTypeSheet row) ? row.Map.RowId : 0u;
    }

    private void Scan()
    {
        LayoutManager* layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null || !layout->InstancesByType.TryGetValue(InstanceType.MapRange, out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> mapPtr, false))
        {
            return;
        }

        List<MapRange> found = [];

        foreach(ILayoutInstance* instance in mapPtr.Value->Values)
        {
            MapRangeLayoutInstance* range = (MapRangeLayoutInstance*)instance;
            if (range->Map == 0)
            {
                continue;
            }

            Transform* transform = instance->GetTransformImpl();
            if (transform == null)
            {
                continue;
            }

            found.Add(new(range->Map, transform->Translation, transform->Rotation, Vector3.Abs(transform->Scale), range->Type));
        }

        if (found.Count == 0)
        {
            return;
        }

        ranges = found.OrderBy(r => r.Volume).ToList();
        hasSubMaps = ranges.Select(r => r.MapId).Append(baseMapId).Distinct().Count() > 1;

        log.Information(
            "[Sub-maps] {Ranges} map range(s) in territory {Territory} covering {Maps} map(s); base map {Base}.",
            ranges.Count,
            scannedTerritory,
            ranges.Select(r => r.MapId).Distinct().Count(),
            baseMapId);
    }

    /// <summary>
    ///     Checks our reading against the one position the game will tell us the answer for: where the player is
    ///     standing. Everything here rests on trigger volumes being matched the way the game matches them, which is not
    ///     something a zone's data can confirm — so it is confirmed against the game, every couple of seconds, for free.
    /// </summary>
    private void Verify()
    {
        if (distrusted || ranges.Count == 0 || clientState.MapId == 0 || objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (!EzThrottler.Throttle("BOCCHI.SubMap.Verify", 2000))
        {
            return;
        }

        uint resolved = Resolve(player.Position) ?? baseMapId;
        if (resolved == clientState.MapId)
        {
            disagreements = 0;
            return;
        }

        if (++disagreements < DisagreementsBeforeDistrust)
        {
            return;
        }

        distrusted = true;
        log.Warning(
            "[Sub-maps] Reading map {Resolved} at {Position} but the game has the player on map {Actual}; "
            + "falling back to treating territory {Territory} as one map.",
            resolved,
            player.Position,
            clientState.MapId,
            scannedTerritory);
    }
}
