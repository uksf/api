using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModUninstallConsumer(IUninstallOperation uninstallOperation, IUksfLogger logger) : IConsumer<WorkshopModUninstallInternalCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModUninstallInternalCommand> context)
    {
        try
        {
            var result = await uninstallOperation.UninstallAsync(context.Message.WorkshopModId, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(new WorkshopModUninstallComplete { WorkshopModId = context.Message.WorkshopModId, FilesChanged = result.FilesChanged });
            }
            else
            {
                logger.LogError($"Uninstall failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "Uninstalling"
                    }
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Uninstall cancelled for {context.Message.WorkshopModId}");
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = "Operation cancelled",
                    FaultedState = "Uninstalling"
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in uninstall consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "Uninstalling"
                }
            );
        }
    }
}
