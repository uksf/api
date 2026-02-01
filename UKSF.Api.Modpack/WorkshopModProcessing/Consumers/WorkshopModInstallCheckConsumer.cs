using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModInstallCheckConsumer(IInstallOperation installOperation, IUksfLogger logger) : IConsumer<WorkshopModInstallCheckCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModInstallCheckCommand> context)
    {
        try
        {
            var result = await installOperation.CheckAsync(context.Message.WorkshopModId, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(
                    new WorkshopModInstallCheckComplete { WorkshopModId = context.Message.WorkshopModId, InterventionRequired = result.InterventionRequired }
                );
            }
            else
            {
                logger.LogError($"Install check failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "InstallingChecking"
                    }
                );
            }
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in install check consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "InstallingChecking"
                }
            );
        }
    }
}
