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
    IVariablesService variablesService
) : IDiscordActivationService
{
    public async Task Activate()
    {
        discordClientService.OnClientReady += OnClientReady;

        await discordClientService.Connect();
        foreach (var discordService in discordServices)
        {
            discordService.Activate();
        }
    }

    public async Task Deactivate()
    {
        await discordClientService.Disconnect();
    }

    private void OnClientReady()
    {
        var createCommandsEnabled = variablesService.GetFeatureState("DISCORD_CREATE_COMMANDS");
        if (!createCommandsEnabled)
        {
            return;
        }

        foreach (var discordService in discordServices)
        {
            discordService.CreateCommands();
        }
    }
}
