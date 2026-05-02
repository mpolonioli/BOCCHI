using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones.Graph;

namespace BOCCHI.Common.Data.Zones;

public interface IZone
{
    bool IsOccultCrescentZone();

    bool IsInBasecamp();

    AethernetData GetMainAetheryte();

    Vector3 GetAetherytePosition();

    Vector3 GetStartingPosition();

    List<AethernetData> GetAetherytes();

    List<AethernetData> GetAethernetShards();

    List<AethernetData> GetNearbyAethernetShards();

    bool HasNearbyAethernetShards()
    {
        return GetNearbyKnowledgeCrystals().Count != 0;
    }

    List<KnowledgeCrystalData> GetKnowledgeCrystals();

    List<KnowledgeCrystalData> GetNearbyKnowledgeCrystals();

    bool HasNearbyKnowledgeCrystals()
    {
        return GetNearbyKnowledgeCrystals().Count != 0;
    }

    bool IsInForkedTower();

    List<ActivityData> GetNormalFateData()
    {
        return [];
    }

    List<ActivityData> GetPotFateData()
    {
        return [];
    }

    List<ActivityData> GetCriticalEncounterData()
    {
        return [];
    }

    List<TreasureData> GetTreasureData()
    {
        return [];
    }

    Dictionary<int, List<PotChestData>> GetPotChestData()
    {
        return [];
    }

    List<PotChestData> GetRerollPotChestData()
    {
        return [];
    }

    List<CarrotData> GetCarrotData()
    {
        return [];
    }

    Task<ZoneGraph> GetGraph();
}
