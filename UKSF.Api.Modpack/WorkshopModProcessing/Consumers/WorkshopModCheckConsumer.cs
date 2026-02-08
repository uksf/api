using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModCheckConsumer(IWorkshopModOperation operation, IUksfLogger logger) : IConsumer<WorkshopModCheckCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModCheckCommand> context)
    {
        try
        {
            var result = await operation.CheckAsync(context.Message.WorkshopModId, context.Message.OperationType, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(
                    new WorkshopModCheckComplete
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        InterventionRequired = result.InterventionRequired,
                        AvailablePbos = result.AvailablePbos
                    }
                );
            }
            else
            {
                logger.LogError($"Check failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "Checking"
                    }
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Check cancelled for {context.Message.WorkshopModId}");
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = "Operation cancelled",
                    FaultedState = "Checking"
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in check consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "Checking"
                }
            );
        }
    }
}
