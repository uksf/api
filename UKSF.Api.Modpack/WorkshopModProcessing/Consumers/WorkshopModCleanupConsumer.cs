using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
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
            var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == context.Message.WorkshopModId);
            if (workshopMod == null)
            {
                logger.LogWarning($"Workshop mod {context.Message.WorkshopModId} not found for cleanup");
                await context.Publish(new WorkshopModCleanupComplete { WorkshopModId = context.Message.WorkshopModId });
                return;
            }

            var workshopModPath = workshopModsProcessingService.GetWorkshopModPath(workshopMod.SteamId);
            workshopModsProcessingService.CleanupWorkshopModFiles(workshopModPath);

            if (context.Message.FilesChanged)
            {
                await workshopModsProcessingService.QueueDevBuild();
            }
            else
            {
                logger.LogInfo($"Skipping dev build for {context.Message.WorkshopModId} - no file changes");
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
