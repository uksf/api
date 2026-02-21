using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Consumers;

public static class ConsumerHelper
{
    public static async Task RunOperationStep(
        ConsumeContext context,
        string workshopModId,
        string stepName,
        Func<Task<OperationResult>> operation,
        Func<OperationResult, Task> onSuccess,
        IWorkshopModsProcessingService processingService,
        IWorkshopModsContext workshopModsContext,
        IUksfLogger logger
    )
    {
        OperationResult result;

        try
        {
            result = await operation();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"{stepName} cancelled for {workshopModId}");
            await SetErrorStatusIfModExists(workshopModId, "Operation cancelled", processingService, workshopModsContext);
            await PublishFault(context, workshopModId, "Operation cancelled", stepName);
            return;
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error during {stepName} for {workshopModId}", exception);
            await SetErrorStatusIfModExists(workshopModId, exception.Message, processingService, workshopModsContext);
            await PublishFault(context, workshopModId, exception.Message, stepName);
            return;
        }

        if (result.Success)
        {
            await onSuccess(result);
            return;
        }

        logger.LogError($"{stepName} failed for {workshopModId}: {result.ErrorMessage}");
        await SetErrorStatusIfModExists(workshopModId, result.ErrorMessage, processingService, workshopModsContext);
        await PublishFault(context, workshopModId, result.ErrorMessage, stepName);
    }

    private static async Task SetErrorStatusIfModExists(
        string workshopModId,
        string errorMessage,
        IWorkshopModsProcessingService processingService,
        IWorkshopModsContext workshopModsContext
    )
    {
        var mod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (mod is not null)
        {
            await processingService.UpdateModStatus(mod, WorkshopModStatus.Error, errorMessage);
        }
    }

    private static Task PublishFault(ConsumeContext context, string workshopModId, string errorMessage, string stepName)
    {
        return context.Publish(
            new WorkshopModOperationFaulted
            {
                WorkshopModId = workshopModId,
                ErrorMessage = errorMessage,
                FaultedState = stepName
            }
        );
    }
}
