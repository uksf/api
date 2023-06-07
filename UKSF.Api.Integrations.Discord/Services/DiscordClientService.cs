using System.Net.WebSockets;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

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
    private readonly IUksfLogger _logger;
    private readonly IVariablesService _variablesService;
    private bool _connected;
    private SocketGuild _guild;

    public DiscordClientService(IOptions<AppSettings> options, DiscordSocketClient client, IVariablesService variablesService, IUksfLogger logger)
    {
        _client = client;
        _variablesService = variablesService;
        _logger = logger;

        var appSettings = options.Value;
        _botToken = appSettings.Secrets.Discord.BotToken;

        _client.Log += OnClientLog;
        _client.Ready += OnClientOnReady;
        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;
        _client.LoggedIn += () =>
        {
            ConnectionLog("Discord logged in");
            return Task.CompletedTask;
        };
        _client.LoggedOut += () =>
        {
            ConnectionLog("Discord logged out");
            return Task.CompletedTask;
        };
    }

    public async Task Connect()
    {
        ConnectionLog("Discord connecting");

        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();
    }

    public async Task Disconnect()
    {
        ConnectionLog("Discord disconnecting");

        await _client.LogoutAsync();
        await _client.StopAsync();
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
            await Task.Delay(15);
            tries++;

            if (tries >= 4)
            {
                _logger.LogError($"Discord failed to reconnect itself after 60 seconds, trying to reconnect. Connection state: {_client.ConnectionState}");
                await Connect();
            }
        }
    }

    public void Dispose()
    {
        Disconnect().Wait();
    }

    private Task OnClientLog(LogMessage logMessage)
    {
        if (logMessage.Exception is not null && logMessage.Severity is LogSeverity.Critical or LogSeverity.Error)
        {
            _logger.LogError(logMessage.Message);
            _logger.LogError(logMessage.Exception);
            return Task.CompletedTask;
        }

        if (logMessage.Severity is LogSeverity.Warning &&
            logMessage.Exception is not GatewayReconnectException &&
            logMessage.Exception?.InnerException is not WebSocketException)
        {
            _logger.LogInfo($"Discord warning log: {logMessage.Message}, {logMessage.Source}, {logMessage.Exception}");
        }

        return Task.CompletedTask;
    }

    private Task OnClientConnected()
    {
        _connected = true;

        ConnectionLog("Discord connected");
        return Task.CompletedTask;
    }

    private Task OnClientOnReady()
    {
        _guild = _client.GetGuild(_variablesService.GetVariable("DID_SERVER").AsUlong());

        ConnectionLog("Discord ready");
        return Task.CompletedTask;
    }

    private Task OnClientDisconnected(Exception exception)
    {
        _connected = false;

        if (IsDiscordConnectionLoggingEnabled())
        {
            _logger.LogError("Discord disconnected");
            _logger.LogError(exception);
        }

        return Task.CompletedTask;
    }

    private void ConnectionLog(string message)
    {
        if (IsDiscordConnectionLoggingEnabled())
        {
            _logger.LogInfo(message);
        }
    }

    private bool IsDiscordConnectionLoggingEnabled()
    {
        return _variablesService.GetVariable("DISCORD_CONNECTION_LOGGING").AsBoolWithDefault(false);
    }
}
