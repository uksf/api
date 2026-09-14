using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public class WorkshopModCleanupConsumer(
    IWorkshopModsProcessingService workshopModsProcessingService,
    IWorkshopModsContext workshopModsContext,
    IUksfLogger logger
) : IConsumer<WorkshopModCleanupCommand>
{
    public async Task Consume(ConsumeContext<WorkshopModCleanupCommand> context)
    {
        try
        {
            // A mod uninstalled before it was ever released is deleted from the database, so the mod is only used for naming here.
            var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == context.Message.WorkshopModId);
            var workshopModPath = workshopModsProcessingService.GetWorkshopModPath(context.Message.WorkshopModId);
            workshopModsProcessingService.CleanupWorkshopModFiles(workshopModPath);

            if (context.Message.FilesChanged)
            {
                await workshopModsProcessingService.QueueDevBuild(
                    workshopMod?.Name ?? $"Workshop mod {context.Message.WorkshopModId}",
                    workshopMod?.Status ?? WorkshopModStatus.Uninstalled
                );
            }

            await context.Publish(new WorkshopModCleanupComplete { WorkshopModId = context.Message.WorkshopModId });
        }
        catch (Exception exception)
        {
            logger.LogError($"Cleanup failed for {context.Message.WorkshopModId}, but continuing", exception);
            // Don't throw - cleanup failure shouldn't prevent saga completion
            await context.Publish(new WorkshopModCleanupComplete { WorkshopModId = context.Message.WorkshopModId });
        }
    }
}
