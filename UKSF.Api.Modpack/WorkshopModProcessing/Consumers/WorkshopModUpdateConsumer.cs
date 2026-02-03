using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModUpdateConsumer(IUpdateOperation updateOperation, IUksfLogger logger) : IConsumer<WorkshopModUpdateInternalCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModUpdateInternalCommand> context)
    {
        try
        {
            var result = await updateOperation.UpdateAsync(context.Message.WorkshopModId, context.Message.SelectedPbos, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(new WorkshopModUpdateComplete { WorkshopModId = context.Message.WorkshopModId, FilesChanged = true });
            }
            else
            {
                logger.LogError($"Update failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "Updating"
                    }
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Update cancelled for {context.Message.WorkshopModId}");
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = "Operation cancelled",
                    FaultedState = "Updating"
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in update consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "Updating"
                }
            );
        }
    }
}
