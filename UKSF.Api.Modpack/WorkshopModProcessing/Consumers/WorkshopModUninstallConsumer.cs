using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModUninstallConsumer(
    IUninstallOperation uninstallOperation,
    IWorkshopModsProcessingService processingService,
    IWorkshopModsContext workshopModsContext,
    IUksfLogger logger
) : IConsumer<WorkshopModUninstallInternalCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModUninstallInternalCommand> context)
    {
        var workshopModName = workshopModsContext.GetSingle(x => x.SteamId == context.Message.WorkshopModId)?.Name;
        await ConsumerHelper.RunOperationStep(
            context,
            context.Message.WorkshopModId,
            "Uninstalling",
            () => uninstallOperation.ExecuteAsync(context.Message.WorkshopModId, [], [], context.CancellationToken),
            result => context.Publish(
                new WorkshopModUninstallComplete
                {
                    WorkshopModId = context.Message.WorkshopModId,
                    FilesChanged = result.FilesChanged,
                    WorkshopModName = workshopModName
                }
            ),
            processingService,
            workshopModsContext,
            logger
        );
    }
}
