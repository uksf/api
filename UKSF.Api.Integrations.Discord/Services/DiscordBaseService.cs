using Discord;
using Discord.WebSocket;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Discord.Models;

namespace UKSF.Api.Discord.Services;

public class DiscordBaseService : IDiscordService
{
    private readonly IDiscordClientService _discordClientService;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly IVariablesService _variablesService;

    protected DiscordBaseService(
        IDiscordClientService discordClientService,
        IHttpContextService httpContextService,
        IVariablesService variablesService,
        IUksfLogger logger
    )
    {
        _discordClientService = discordClientService;
        _httpContextService = httpContextService;
        _variablesService = variablesService;
        _logger = logger;
    }

    public virtual void Activate() { }

    protected DiscordSocketClient GetClient()
    {
        return _discordClientService.GetClient();
    }

    protected SocketGuild GetGuild()
    {
        return _discordClientService.GetGuild();
    }

    protected Task AssertOnline()
    {
        return _discordClientService.AssertOnline();
    }

    protected bool IsDiscordDisabled()
    {
        return _discordClientService.IsDiscordDisabled();
    }

    protected Task WrapEventTask(Func<Task> actionTask)
    {
        if (IsDiscordDisabled())
        {
            return Task.CompletedTask;
        }

        Task.Run(async () => { await RunEventTask(actionTask); });

        return Task.CompletedTask;
    }

    protected Task WrapAdminEventTask(Func<Task> actionTask)
    {
        Task.Run(async () => { await RunEventTask(actionTask); });

        return Task.CompletedTask;
    }

    private async Task RunEventTask(Func<Task> actionTask)
    {
        var discordAccountId = _variablesService.GetVariable("DISCORD_BOT_ACCOUNT_ID").AsString();
        _httpContextService.SetContextId(discordAccountId);
        try
        {
            await actionTask();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception);
            throw;
        }
    }

    protected static string GetUserNickname(IGuildUser user)
    {
        if (user == null)
        {
            return "";
        }

        return string.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname;
    }

    protected bool IsAccountOnline(ulong discordId)
    {
        return GetGuild().GetUser(discordId)?.Status == UserStatus.Online;
    }

    protected string GetAccountNickname(ulong discordId)
    {
        var user = GetGuild().GetUser(discordId);
        return GetUserNickname(user);
    }

    protected static string BuildButtonData(string customId, params string[] data)
    {
        return $"{customId}:{string.Join(":", data)}";
    }

    protected static DiscordButtonData GetButtonData(string customId)
    {
        var dataParts = customId.Split(':');

        return new() { Id = dataParts[0], Data = dataParts.Skip(1).ToList() };
    }
}
