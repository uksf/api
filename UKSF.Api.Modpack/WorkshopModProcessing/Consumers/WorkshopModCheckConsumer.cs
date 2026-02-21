using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModCheckConsumer(
    IInstallOperation installOperation,
    IUpdateOperation updateOperation,
    IWorkshopModsProcessingService processingService,
    IWorkshopModsContext workshopModsContext,
    IUksfLogger logger
) : IConsumer<WorkshopModCheckCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModCheckCommand> context)
    {
        IModOperation operation = context.Message.OperationType == WorkshopModOperationType.Install ? installOperation : updateOperation;
        await ConsumerHelper.RunOperationStep(
            context,
            context.Message.WorkshopModId,
            "Checking",
            () => operation.CheckAsync(context.Message.WorkshopModId, context.CancellationToken),
            result => context.Publish(
                new WorkshopModCheckComplete
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    InterventionRequired = result.InterventionRequired,
                    AvailablePbos = result.AvailablePbos
                }
            ),
            processingService,
            workshopModsContext,
            logger
        );
    }
}
