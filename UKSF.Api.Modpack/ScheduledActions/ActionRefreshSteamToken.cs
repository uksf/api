using UKSF.Api.Core;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.ScheduledActions;

public interface IActionRefreshSteamToken : ISelfCreatingScheduledAction;

public class ActionRefreshSteamToken(
    ISchedulerService schedulerService,
    IHostEnvironment currentEnvironment,
    IClock clock,
    ISteamCmdService steamCmdService,
    IUksfLogger logger
) : SelfCreatingScheduledAction(schedulerService, currentEnvironment), IActionRefreshSteamToken
{
    private const string ActionName = nameof(ActionRefreshSteamToken);

    public override DateTime NextRun => clock.UkToday().AddHours(6);
    public override TimeSpan RunInterval => TimeSpan.FromHours(12);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        try
        {
            var output = await steamCmdService.RefreshLogin();

            if (output.Contains("Steam Guard", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("Account Logon Denied", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError($"Steam Guard code required — manual re-authentication needed. SteamCMD output:\n{output}");
                throw new InvalidOperationException("Steam Guard code required — manual re-authentication needed");
            }

            if (output.Contains("FAILED", StringComparison.OrdinalIgnoreCase) || output.Contains("Login Failure", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError($"Steam login failed. SteamCMD output:\n{output}");
                throw new InvalidOperationException($"Steam login failed: {output}");
            }

            logger.LogInfo("Steam token refreshed successfully");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to refresh Steam token", exception);
            throw;
        }
    }
}
