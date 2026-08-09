using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Treasure.Services;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using System.Numerics;
using System.Runtime.CompilerServices;
using GameTreasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;
using TreasureFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags;
using TreasureSheet = Lumina.Excel.Sheets.Treasure;

namespace BOCCHI.Treasure.Guided;

/// <summary>
///     Holds the zone's coffer spawn points and keeps their status current as the player moves past them.
///     <para>
///         Spawn points come from the live layout rather than a shipped table, the same way the precompute panel and the
///         automated hunt read them, so a zone needs no extra data to be guided.
///     </para>
/// </summary>
public class GuidedCofferTracker(
    IClientState clientState,
    IObjectTable objects,
    IDataManager data,
    IZoneProvider zones,
    ISubMapResolver subMaps,
    IPluginLog log
) : GuidedSpotTracker<CofferSpot>(objects, zones, subMaps, log)
{
    protected override string Name => "treasure";

    /// <summary>Coffers spawn on the point, so this only absorbs the drift between the layout transform and the object.</summary>
    protected override float MatchRadius => 6f;

    /// <summary>Territory the current <see cref="GuidedSpotTracker{TSpot}.Spots" /> were read from, so a zone change forces a rescan.</summary>
    private uint scannedTerritory = uint.MaxValue;

    /// <summary>Coffers this session watched go from present to opened — i.e. ones actually banked while guiding.</summary>
    public int CoffersOpened { get; private set; }

    public int Count(TreasureType type)
    {
        return Spots.Count(s => s.Type == type);
    }

    public int Count(TreasureType type, SpotStatus status)
    {
        return Spots.Count(s => s.Type == type && s.Status == status);
    }

    /// <summary>
    ///     Rescans on a zone change, and retries on an interval while the scan comes up empty. The layout is not
    ///     populated the instant the territory id flips, so the first scan after a zone load routinely finds nothing —
    ///     which is why this retries rather than treating one empty read as "this zone has no coffers".
    /// </summary>
    protected override void MaintainSpots()
    {
        uint territory = clientState.TerritoryType;

        if (territory != scannedTerritory && Spots.Count > 0)
        {
            // Nothing learned about the last zone means anything here, and leaving its spots up would have the hunt
            // pointing at coordinates in another map.
            Spots = [];
            CoffersOpened = 0;
            SessionStartedAt = DateTime.Now;
        }

        if (territory == scannedTerritory && Spots.Count > 0)
        {
            return;
        }

        if (Throttle("Rescan", 1000))
        {
            ScanLayout();
        }
    }

    protected override unsafe List<Sighting> ScanWorld()
    {
        List<Sighting> sightings = [];

        foreach(Dalamud.Game.ClientState.Objects.Types.IGameObject obj in Objects)
        {
            if (obj.ObjectKind != ObjectKind.Treasure || !obj.IsValid())
            {
                continue;
            }

            GameTreasure* coffer = (GameTreasure*)(void*)obj.Address;
            if (coffer == null)
            {
                continue;
            }

            sightings.Add(new(obj.Position, coffer->Flags.HasFlag(TreasureFlags.Opened) ? SpotStatus.Opened : SpotStatus.Present));
        }

        return sightings;
    }

    protected override void AfterObserve(IReadOnlyList<(CofferSpot Spot, SpotStatus Previous)> changes)
    {
        foreach((CofferSpot spot, SpotStatus previous) in changes)
        {
            if (previous == SpotStatus.Present && spot.Status == SpotStatus.Opened)
            {
                CoffersOpened++;
            }
        }
    }

    public void Rescan()
    {
        ScanLayout();
    }

    private unsafe void ScanLayout()
    {
        Dictionary<uint, CofferSpot> previous = Spots.ToDictionary(s => s.Id, s => s);
        uint territory = clientState.TerritoryType;
        List<CofferSpot> spots = [];

        LayoutManager* layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null || !layout->InstancesByType.TryGetValue(InstanceType.Treasure, out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> mapPtr, false))
        {
            // Not an error: this is the normal state for the first frames after a zone load, and MaintainSpots retries.
            return;
        }

        List<TreasureData> authored = Zones.GetZone().GetTreasureData();

        foreach(ILayoutInstance* instance in mapPtr.Value->Values)
        {
            Vector3 position = instance->GetTransformImpl()->Translation;
            if (!TreasureLayout.IsInPlayableZone(position))
            {
                continue;
            }

            uint treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
            uint sgbId = data.GetExcelSheet<TreasureSheet>().GetRow(treasureRowId).SGB.RowId;
            if (!TreasureCoffer.IsBronzeOrSilverSgb(sgbId))
            {
                continue;
            }

            TreasureType type = sgbId == TreasureCoffer.SilverSgbId ? TreasureType.Silver : TreasureType.Bronze;
            TreasureData? match = authored.FirstOrDefault(d => d.Matches(treasureRowId, position));
            spots.Add(new(treasureRowId, position, type, (uint)Math.Max(0, match?.Level ?? 0)));
        }

        if (spots.Count == 0)
        {
            return;
        }

        Spots = spots.OrderBy(s => s.Id).ToList();
        scannedTerritory = territory;

        // A rescan of the zone we are already in — the retry path, or the button — keeps what has been learned. A zone
        // change does not, because previous holds another zone's ids entirely and nothing would match anyway.
        foreach(CofferSpot spot in Spots)
        {
            if (previous.TryGetValue(spot.Id, out CofferSpot? old) && old.Status != SpotStatus.Unknown)
            {
                spot.Mark(old.Status);
            }
        }

        Log.Information(
            "[Guided treasure] {Count} coffer spawn point(s) ({Silver} silver, {Bronze} bronze).",
            Spots.Count,
            Count(TreasureType.Silver),
            Count(TreasureType.Bronze));
    }

    public override void ResetChecks()
    {
        base.ResetChecks();
        CoffersOpened = 0;
    }
}
