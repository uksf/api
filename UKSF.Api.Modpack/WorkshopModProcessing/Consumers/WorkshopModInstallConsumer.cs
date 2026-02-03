using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModInstallConsumer(IInstallOperation installOperation, IUksfLogger logger) : IConsumer<WorkshopModInstallInternalCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModInstallInternalCommand> context)
    {
        try
        {
            var result = await installOperation.InstallAsync(context.Message.WorkshopModId, context.Message.SelectedPbos, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(new WorkshopModInstallComplete { WorkshopModId = context.Message.WorkshopModId, FilesChanged = true });
            }
            else
            {
                logger.LogError($"Install failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "Installing"
                    }
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Install cancelled for {context.Message.WorkshopModId}");
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = "Operation cancelled",
                    FaultedState = "Installing"
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in install consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "Installing"
                }
            );
        }
    }
}
