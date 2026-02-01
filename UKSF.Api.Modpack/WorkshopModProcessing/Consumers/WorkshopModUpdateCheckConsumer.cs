using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModUpdateCheckConsumer(IUpdateOperation updateOperation, IWorkshopModsContext workshopModsContext, IUksfLogger logger)
    : IConsumer<WorkshopModUpdateCheckCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModUpdateCheckCommand> context)
    {
        try
        {
            var result = await updateOperation.CheckAsync(context.Message.WorkshopModId, context.CancellationToken);
            if (result.Success)
            {
                await context.Publish(
                    new WorkshopModUpdateCheckComplete { WorkshopModId = context.Message.WorkshopModId, InterventionRequired = result.InterventionRequired }
                );
            }
            else
            {
                logger.LogError($"Update check failed for {context.Message.WorkshopModId}: {result.ErrorMessage}");
                await context.Publish(
                    new WorkshopModOperationFaulted
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        ErrorMessage = result.ErrorMessage,
                        FaultedState = "UpdatingChecking"
                    }
                );
            }
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error in update check consumer for {context.Message.WorkshopModId}", exception);
            await context.Publish(
                new WorkshopModOperationFaulted
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    ErrorMessage = exception.Message,
                    FaultedState = "UpdatingChecking"
                }
            );
        }
    }
}
