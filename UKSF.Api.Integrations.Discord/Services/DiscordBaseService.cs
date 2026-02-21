using Discord;
using Discord.WebSocket;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Discord.Models;

namespace UKSF.Api.Integrations.Discord.Services;

public interface IDiscordService
{
    void Activate();
    Task CreateCommands();
}

public class DiscordBaseService(
    IDiscordClientService discordClientService,
    IAccountContext accountContext,
    IHttpContextService httpContextService,
    IVariablesService variablesService,
    IUksfLogger logger
)
{
    protected DiscordSocketClient GetClient()
    {
        return discordClientService.GetClient();
    }

    protected SocketGuild GetGuild()
    {
        return discordClientService.GetGuild();
    }

    protected Task AssertOnline()
    {
        return discordClientService.AssertOnline();
    }

    protected bool IsDiscordDisabled()
    {
        return discordClientService.IsDiscordDisabled();
    }

    protected Task WrapEventTask(Func<Task> actionTask)
    {
        if (IsDiscordDisabled())
        {
            return Task.CompletedTask;
        }

        _ = Task.Run(async () => { await RunEventTask(actionTask); });

        return Task.CompletedTask;
    }

    protected Task WrapAdminEventTask(Func<Task> actionTask)
    {
        _ = Task.Run(async () => { await RunEventTask(actionTask); });

        return Task.CompletedTask;
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

        return new DiscordButtonData { Id = dataParts[0], Data = dataParts.Skip(1).ToList() };
    }

    protected DomainAccount GetAccountForDiscordUser(ulong userId)
    {
        return accountContext.GetSingle(x => x.DiscordId == userId.ToString());
    }

    protected void SetUserContextByDiscordUser(ulong userId)
    {
        var account = GetAccountForDiscordUser(userId);
        httpContextService.SetContextId(account.Id);
    }

    private async Task RunEventTask(Func<Task> actionTask)
    {
        var discordAccountId = variablesService.GetVariable("DISCORD_BOT_ACCOUNT_ID").AsString();
        httpContextService.SetContextId(discordAccountId);
        try
        {
            await actionTask();
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
            throw;
        }
    }
}
