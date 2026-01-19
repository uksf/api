using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModUpdateDownloadConsumer(IUpdateOperation updateOperation, IUksfLogger logger) : IConsumer<WorkshopModUpdateDownloadCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModUpdateDownloadCommand> context)
    {
        try
        {
            var result = await updateOperation.DownloadAsync(context.Message.WorkshopModId, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(new WorkshopModUpdateDownloadComplete { WorkshopModId = context.Message.WorkshopModId });
            }
            else
            {
                logger.LogError($"Update download failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "UpdatingDownloading"
                    }
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Update download cancelled for {context.Message.WorkshopModId}");
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = "Operation cancelled",
                    FaultedState = "UpdatingDownloading"
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in update download consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "UpdatingDownloading"
                }
            );
        }
    }
}
