using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Ocelot.Extensions;

namespace BOCCHI.Common.Services;

public interface ICriticalEncounterContext
{
    bool IsInCriticalEncounter();

    CriticalEncounterId? GetCriticalEncounterId();

    bool IsInCeZone(IPlayerCharacter player, CriticalEncounter encounter)
    {
        return player.Position.Distance2D(encounter.Position) <= encounter.Radius;
    }
}
