using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InCriticalEncounterHandler(
    IAutomatorMemory memory,
    ICriticalEncounterContext context,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    CombatConfig combat,
    ITargetManager targetManager
    ) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InCriticalEncounter)
{
    public override StatePriority GetScore()
    {
        return context.IsInCriticalEncounter() ? StatePriority.High : StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();
        memory.Forget<WaitingForCriticalEncounterMemory>();
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        var targets = context.GetTargets().ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var target = targets.First();
        if (combat.ShouldHandleTargeting && EzThrottler.Throttle("InCriticalEncounter::Target") && targetManager.Target?.GameObjectId != target.GameObjectId)
        {
            targetManager.Target = target;
        }

        var distance = player.Position.Distance2D(target.Position) - target.HitboxRadius;
        if (distance <= 5f && conditions[ConditionFlag.Mounted])
        {
            if (EzThrottler.Throttle("InCriticalEncounter::Unmount") && Actions.Unmount.CanCast())
            {
                Actions.Unmount.Cast();
                pathfinder.Stop();
            }
        }
    }
}
