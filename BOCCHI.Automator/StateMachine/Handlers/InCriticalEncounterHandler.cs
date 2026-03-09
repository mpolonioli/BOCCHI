using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InCriticalEncounterHandler(
    IAutomatorMemory memory,
    ICriticalEncounterContext context,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
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

        // var targets = context.GetFateTargets().ToList();
        // if (targets.Count == 0)
        // {
        //     return;
        // }
        //
        // var target = targets.First();
        // if (EzThrottler.Throttle("InFate::Target") && targetManager.Target == null)
        // {
        //     targetManager.Target = target;
        // }

        // var distance = player.Position.Distance2D(target.Position) - target.HitboxRadius;
        // if (distance <= 5f && conditions[ConditionFlag.Mounted])
        // {
        //     if (EzThrottler.Throttle("InFate::Unmount") && Actions.Unmount.CanCast())
        //     {
        //         Actions.Unmount.Cast();
        //         pathfinder.Stop();
        //     }
        // }
    }
}
