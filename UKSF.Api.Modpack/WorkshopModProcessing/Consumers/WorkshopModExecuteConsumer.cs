using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModExecuteConsumer(
    IInstallOperation installOperation,
    IUpdateOperation updateOperation,
    IWorkshopModsProcessingService processingService,
    IWorkshopModsContext workshopModsContext,
    IUksfLogger logger
) : IConsumer<WorkshopModExecuteCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModExecuteCommand> context)
    {
        IModOperation operation = context.Message.OperationType == WorkshopModOperationType.Install ? installOperation : updateOperation;
        await ConsumerHelper.RunOperationStep(
            context,
            context.Message.WorkshopModId,
            "Executing",
            () => operation.ExecuteAsync(context.Message.WorkshopModId, context.Message.SelectedPbos, context.CancellationToken),
            result => context.Publish(new WorkshopModExecuteComplete { WorkshopModId = context.Message.WorkshopModId, FilesChanged = result.FilesChanged }),
            processingService,
            workshopModsContext,
            logger
        );
    }
}
