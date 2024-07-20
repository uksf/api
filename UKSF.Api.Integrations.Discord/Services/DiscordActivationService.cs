namespace UKSF.Api.Integrations.Discord.Services;

public interface IDiscordActivationService
{
    Task Activate();
    Task Deactivate();
}

public class DiscordActivationService(IDiscordClientService discordClientService, IEnumerable<IDiscordService> discordServices) : IDiscordActivationService
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

    private void OnClientReady()
    {
        foreach (var discordService in discordServices)
        {
            discordService.CreateCommands();
        }
    }

    public async Task Deactivate()
    {
        await discordClientService.Disconnect();
    }
}
