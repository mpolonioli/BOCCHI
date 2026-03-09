using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace BOCCHI.CriticalEncounters.Services;

public class CriticalEncounterContext : ICriticalEncounterContext
{
    public bool IsInCriticalEncounter()
    {
        return GetCriticalEncounterId() != null;
    }

    public unsafe CriticalEncounterId? GetCriticalEncounterId()
    {
        var dec = DynamicEventContainer.GetInstance();
        return dec != null && dec->CurrentEventId != 0 ? new CriticalEncounterId(dec->CurrentEventId) : null;
    }
}
