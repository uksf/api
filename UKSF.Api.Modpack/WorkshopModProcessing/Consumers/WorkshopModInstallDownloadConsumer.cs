using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModInstallDownloadConsumer(IInstallOperation installOperation, IUksfLogger logger) : IConsumer<WorkshopModInstallDownloadCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModInstallDownloadCommand> context)
    {
        try
        {
            var result = await installOperation.DownloadAsync(context.Message.WorkshopModId, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(new WorkshopModInstallDownloadComplete { WorkshopModId = context.Message.WorkshopModId });
            }
            else
            {
                logger.LogError($"Install download failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "InstallingDownloading"
                    }
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Install download cancelled for {context.Message.WorkshopModId}");
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = "Operation cancelled",
                    FaultedState = "InstallingDownloading"
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in install download consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "InstallingDownloading"
                }
            );
        }
    }
}
