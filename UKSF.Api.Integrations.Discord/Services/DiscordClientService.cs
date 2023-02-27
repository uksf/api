using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Discord.Exceptions;

namespace UKSF.Api.Discord.Services;

public interface IDiscordClientService
{
    Task Connect();
    Task Disconnect();
    DiscordSocketClient GetClient();
    SocketGuild GetGuild();
    bool IsDiscordDisabled();
    Task AssertOnline();
}

public sealed class DiscordClientService : IDiscordClientService, IDisposable
{
    private readonly string _botToken;
    private readonly DiscordSocketClient _client;
    private readonly IVariablesService _variablesService;
    private readonly IUksfLogger _logger;
    private bool _connected;
    private SocketGuild _guild;

    public DiscordClientService(IOptions<AppSettings> options, DiscordSocketClient client, IVariablesService variablesService, IUksfLogger logger)
    {
        _client = client;
        _variablesService = variablesService;
        _logger = logger;

        var appSettings = options.Value;
        _botToken = appSettings.Secrets.Discord.BotToken;
    }

    public async Task Connect()
    {
        _logger.LogInfo("Discord connecting");
        _client.Ready += OnClientOnReady;
        _client.Disconnected += ClientOnDisconnected;

        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();
    }

    public Task Disconnect()
    {
        _logger.LogInfo("Discord disconnecting");
        return _client.StopAsync();
    }

    public void Dispose()
    {
        Disconnect().Wait();
    }

    public DiscordSocketClient GetClient()
    {
        return _client;
    }

    public SocketGuild GetGuild()
    {
        return _guild;
    }

    public bool IsDiscordDisabled()
    {
        return !_variablesService.GetFeatureState("DISCORD");
    }

    public async Task AssertOnline()
    {
        if (_connected)
        {
            return;
        }

        var tries = 0;
        while (!_connected)
        {
            await Task.Delay(30);
            tries++;

            if (tries >= 10)
            {
                _logger.LogError("Discord failed to reconnect itself after 5 minutes");
                throw new DiscordOfflineException();
            }
        }
    }

    private Task OnClientOnReady()
    {
        _guild = _client.GetGuild(_variablesService.GetVariable("DID_SERVER").AsUlong());
        _connected = true;

        return Task.CompletedTask;
    }

    private Task ClientOnDisconnected(Exception exception)
    {
        _connected = false;
        return Task.CompletedTask;
    }
}
