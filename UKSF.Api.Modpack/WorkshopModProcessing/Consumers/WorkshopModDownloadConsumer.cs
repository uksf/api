using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModDownloadConsumer(IWorkshopModOperation operation, IUksfLogger logger) : IConsumer<WorkshopModDownloadCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModDownloadCommand> context)
    {
        try
        {
            var result = await operation.DownloadAsync(context.Message.WorkshopModId, context.Message.OperationType, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(new WorkshopModDownloadComplete { WorkshopModId = context.Message.WorkshopModId });
            }
            else
            {
                logger.LogError($"Download failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "Downloading"
                    }
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Download cancelled for {context.Message.WorkshopModId}");
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = "Operation cancelled",
                    FaultedState = "Downloading"
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in download consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "Downloading"
                }
            );
        }
    }
}
