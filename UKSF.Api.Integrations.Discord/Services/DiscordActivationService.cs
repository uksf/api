namespace UKSF.Api.Discord.Services;

public interface IDiscordActivationService
{
    Task Activate();
    Task Deactivate();
}

public class DiscordActivationService : IDiscordActivationService
{
    private readonly IDiscordClientService _discordClientService;

    private readonly IEnumerable<IDiscordService> _discordServices;
    // private readonly InteractionService _interactionService;
    // private readonly IServiceProvider _serviceProvider;

    public DiscordActivationService(IDiscordClientService discordClientService, IEnumerable<IDiscordService> discordServices)
    {
        _discordClientService = discordClientService;
        _discordServices = discordServices;
        // _interactionService = interactionService;
        // _serviceProvider = serviceProvider;
    }

    public async Task Activate()
    {
        // await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider);

        await _discordClientService.Connect();

        foreach (var discordService in _discordServices)
        {
            discordService.Activate();
        }
    }

    public async Task Deactivate()
    {
        await _discordClientService.Disconnect();
    }
}

public interface IDiscordService
{
    void Activate();
}
