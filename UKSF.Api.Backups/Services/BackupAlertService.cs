using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Discord.Services;

namespace UKSF.Api.Backups.Services;

public interface IBackupAlertService
{
    Task Alert(string message);
}

/// <summary>A backup nobody watches is not a backup. Every failure and every missed run reaches Discord.</summary>
public class BackupAlertService(IVariablesService variablesService, IDiscordMessageService discordMessageService, IUksfLogger logger) : IBackupAlertService
{
    private const ulong DefaultChannelId = 707615025380065400;

    public async Task Alert(string message)
    {
        logger.LogError($"Backup alert: {message}");

        try
        {
            var channelId = variablesService.GetVariable("DID_C_BACKUPS").AsUlongWithDefault(DefaultChannelId);
            await discordMessageService.SendMessage(channelId, $":rotating_light: **Backup** - {message}");
        }
        catch (Exception exception)
        {
            // The log above is the fallback; a Discord outage must not swallow the reason the alert was raised.
            logger.LogError($"Backup alert could not be sent to Discord: {exception.Message}");
        }
    }
}
