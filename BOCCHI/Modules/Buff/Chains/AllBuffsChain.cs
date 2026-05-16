using BOCCHI.Data;
using ECommons.Automation.NeoTaskManager;
using Ocelot.Chain;

namespace BOCCHI.Modules.Buff.Chains;

public class AllBuffsChain : ChainFactory
{
    private readonly BuffModule module;

    private readonly Job StartingJob;

    public AllBuffsChain(BuffModule module)
    {
        this.module = module;
        StartingJob = Job.Current;
        module.BuffManager.LockStartingJob(StartingJob);
    }

    protected override Chain Create(Chain chain)
    {
        chain
            .Then(new FreelancerBuffChain(module))
            .Then(new KnightBuffChain(module))
            .Then(new MonkBuffChain(module))
            .Then(new BardBuffChain(module))
            .Then(new DancerBuffChain(module))
            .Then(StartingJob.ChangeToChain)
            .Then(_ =>
            {
                module.BuffManager.ClearStartingJob();
                return true;
            });

        return chain;
    }

    public override TaskManagerConfiguration Config()
    {
        return new TaskManagerConfiguration { TimeLimitMS = 60000 };
    }
}
