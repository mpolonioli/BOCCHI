// This was pretty buggy and needs further work.
//
// using BOCCHI.Automator.Data;
// using BOCCHI.Common.Data.Zones;
// using BOCCHI.Common.Services;
// using Ocelot.Chain;
// using Ocelot.Services.Logger;
// using Ocelot.States.Score;
//
// namespace BOCCHI.Automator.StateMachine.Handlers;
//
// public class ExtractingMateriaHandler(
//     IZoneProvider zones,
//     IMateriaExtractionService materia,
//     IChainManager chains,
//     ILogger<ExtractingMateriaHandler> log
// ) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.ExtractingMateria)
// {
//     private Task<ChainResult>? task;
//
//     public override StatePriority GetScore()
//     {
//         if (task is { IsCompleted: false })
//         {
//             return StatePriority.Always;
//         }
//
//         return zones.GetZone().IsInBasecamp() && materia.ShouldExtract() ? StatePriority.AboveNormal : StatePriority.Never;
//     }
//
//     public override void Enter()
//     {
//         base.Enter();
//
//         task = chains.Manage(materia.ExtractEquipped());
//     }
//
//     public override void Exit(AutomatorState next)
//     {
//         base.Exit(next);
//         if (task is { IsCompleted: true })
//         {
//             task.Dispose();
//             task = null;
//         }
//     }
//
//     public override void Handle()
//     {
//         // Do nothing this is handled by the task
//     }
// }
