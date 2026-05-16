using System;
using System.Numerics;
using BOCCHI.Modules.StateManager;
using BOCCHI.Modules.Treasure;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using Ocelot.Chain;
using Ocelot.IPC;

namespace BOCCHI.Modules.Automator;

public class TreasureHuntActivity : Activity
{
    private readonly TreasureModule treasure;

    private bool started;

    public TreasureHuntActivity(Lifestream lifestream, VNavmesh vnav, AutomatorModule module, TreasureModule treasure)
        : base(default, lifestream, vnav, module)
    {
        this.treasure = treasure;
        state = ActivityState.Idle;
    }

    public override Func<Chain>? GetChain(StateManagerModule states)
    {
        if (!IsValid())
        {
            return null;
        }

        if (!started)
        {
            return () => Chain
                .Create("Illegal:TreasureHunt.Start")
                .Then(_ =>
                {
                    treasure.StartHunt();
                    started = true;
                    state = ActivityState.Participating;
                });
        }

        if (!treasure.IsHunting)
        {
            state = ActivityState.Done;
        }

        return null;
    }

    public override string GetStateLabel()
    {
        if (state == ActivityState.Participating && treasure.HuntTotal > 0)
        {
            return $"{treasure.HuntProgress}/{treasure.HuntTotal}";
        }

        return state.ToLabel();
    }

    public override bool IsValid()
    {
        return state != ActivityState.Done;
    }

    public override string GetName()
    {
        return module.T("panel.activity.treasure_hunt");
    }

    protected override bool IsActivityTarget(IBattleNpc obj)
    {
        return false;
    }

    protected override float GetRadius()
    {
        return 0f;
    }

    protected override TaskManagerTask GetPathfindingWatcher(StateManagerModule states)
    {
        throw new NotSupportedException();
    }

    protected override Vector3 GetPosition()
    {
        return Vector3.Zero;
    }

    protected override ActivityState GetPostPathfindingState()
    {
        return ActivityState.Done;
    }
}
