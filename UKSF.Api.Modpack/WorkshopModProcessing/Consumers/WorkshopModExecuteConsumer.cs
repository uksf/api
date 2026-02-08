using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModExecuteConsumer(IWorkshopModOperation operation, IUksfLogger logger) : IConsumer<WorkshopModExecuteCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModExecuteCommand> context)
    {
        try
        {
            var result = await operation.ExecuteAsync(
                context.Message.WorkshopModId,
                context.Message.OperationType,
                context.Message.SelectedPbos,
                context.CancellationToken
            );
            if (result.Success)
            {
                await context.Publish(
                    new WorkshopModExecuteComplete { WorkshopModId = context.Message.WorkshopModId, FilesChanged = result.FilesChanged }
                );
            }
            else
            {
                logger.LogError($"Execute failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "Executing"
                    }
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Execute cancelled for {context.Message.WorkshopModId}");
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = "Operation cancelled",
                    FaultedState = "Executing"
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in execute consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "Executing"
                }
            );
        }
    }
}
