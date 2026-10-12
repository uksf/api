using System.Linq.Expressions;
using MassTransit;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Core;

namespace UKSF.Api.Modpack.WorkshopModProcessing;

public class WorkshopModSagaRecoveryStartup(
    IQuerySagaRepository<WorkshopModInstanceState> querySagaRepository,
    ILoadSagaRepository<WorkshopModInstanceState> loadSagaRepository,
    IBus bus,
    IUksfLogger logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sagaIds = await querySagaRepository.Find(new AllSagas());
        foreach (var sagaId in sagaIds)
        {
            try
            {
                var saga = await loadSagaRepository.Load(sagaId);
                var command = PendingCommand(saga);
                if (command is null) continue;

                await bus.Publish(command, cancellationToken);
                logger.LogInfo($"Workshop mod {saga.WorkshopModId}: resumed {saga.Operation} in state {saga.CurrentState} after restart");
            }
            catch (Exception exception)
            {
                logger.LogError(exception);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static object PendingCommand(WorkshopModInstanceState saga) =>
        saga.CurrentState switch
        {
            nameof(WorkshopModStateMachine.Downloading) => new WorkshopModDownloadCommand
            {
                WorkshopModId = saga.WorkshopModId, OperationType = saga.OperationType!.Value
            },
            nameof(WorkshopModStateMachine.Checking) => new WorkshopModCheckCommand
            {
                WorkshopModId = saga.WorkshopModId, OperationType = saga.OperationType!.Value
            },
            nameof(WorkshopModStateMachine.Executing) => new WorkshopModExecuteCommand
            {
                WorkshopModId = saga.WorkshopModId,
                OperationType = saga.OperationType!.Value,
                SelectedPbos = saga.SelectedPbos,
                SelectedExtensions = saga.SelectedExtensions
            },
            nameof(WorkshopModStateMachine.Uninstalling) => new WorkshopModUninstallInternalCommand { WorkshopModId = saga.WorkshopModId },
            nameof(WorkshopModStateMachine.Cleanup) => new WorkshopModCleanupCommand
            {
                WorkshopModId = saga.WorkshopModId,
                FilesChanged = saga.FilesChanged,
                WorkshopModName = saga.WorkshopModName
            },
            _ => null
        };

    private class AllSagas : ISagaQuery<WorkshopModInstanceState>
    {
        public Expression<Func<WorkshopModInstanceState, bool>> FilterExpression { get; } = _ => true;

        public Func<WorkshopModInstanceState, bool> GetFilter() => FilterExpression.Compile();
    }
}
