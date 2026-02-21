using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModDownloadConsumer(
    IInstallOperation installOperation,
    IUpdateOperation updateOperation,
    IWorkshopModsProcessingService processingService,
    IWorkshopModsContext workshopModsContext,
    IUksfLogger logger
) : IConsumer<WorkshopModDownloadCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModDownloadCommand> context)
    {
        IModOperation operation = context.Message.OperationType == WorkshopModOperationType.Install ? installOperation : updateOperation;
        await ConsumerHelper.RunOperationStep(
            context,
            context.Message.WorkshopModId,
            "Downloading",
            () => operation.DownloadAsync(context.Message.WorkshopModId, context.CancellationToken),
            _ => context.Publish(new WorkshopModDownloadComplete { WorkshopModId = context.Message.WorkshopModId }),
            processingService,
            workshopModsContext,
            logger
        );
    }
}
