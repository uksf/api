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
                var workshopMod = workshopModsContext.GetSingle(context.Message.WorkshopModId);
                var currentPbos = workshopMod?.Pbos ?? [];

                // Only require intervention if PBOs differ from currently installed
                var pbosChanged = !currentPbos.OrderBy(x => x).SequenceEqual(result.AvailablePbos.OrderBy(x => x));

                await context.Publish(
                    new WorkshopModUpdateCheckComplete
                    {
                        WorkshopModId = context.Message.WorkshopModId,
                        InterventionRequired = pbosChanged,
                        AvailablePbos = result.AvailablePbos
                    }
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
