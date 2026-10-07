using UKSF.Api.Core;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Integrations.Discord.Services;

public interface IDiscordActivationService
{
    Task Activate();
    Task Deactivate();
}

public class DiscordActivationService(
    IDiscordClientService discordClientService,
    IEnumerable<IDiscordService> discordServices,
    IVariablesService variablesService,
    IUksfLogger logger
) : IDiscordActivationService
{
    private bool _connected;

    public async Task Activate()
    {
        if (!variablesService.GetFeatureState("DISCORD"))
        {
            logger.LogInfo("Discord is disabled, the bot will not connect");
            return;
        }

        discordClientService.OnClientReady += OnClientReady;
        _connected = true;

        await discordClientService.Connect();
        foreach (var discordService in discordServices)
        {
            discordService.Activate();
        }
    }

    public async Task Deactivate()
    {
        if (!_connected)
        {
            return;
        }

        _connected = false;
        await discordClientService.Disconnect();
    }

    private void OnClientReady()
    {
        var createCommandsEnabled = variablesService.GetFeatureState("DISCORD_CREATE_COMMANDS");
        if (!createCommandsEnabled)
        {
            return;
        }

        Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(discordServices.Select(s => s.CreateCommands()));
                }
                catch (Exception exception)
                {
                    logger.LogError(exception);
                }
            }
        );
    }
}
