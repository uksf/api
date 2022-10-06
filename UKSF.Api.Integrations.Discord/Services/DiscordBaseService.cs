using Discord;
using Discord.WebSocket;

namespace UKSF.Api.Discord.Services;

public class DiscordBaseService : IDiscordService
{
    private readonly IDiscordClientService _discordClientService;

    protected DiscordBaseService(IDiscordClientService discordClientService)
    {
        _discordClientService = discordClientService;
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

    protected Task WrapEventTask(Func<Task> action)
    {
        if (IsDiscordDisabled())
        {
            return Task.CompletedTask;
        }

        Task.Run(() => { action(); });

        return Task.CompletedTask;
    }

    protected Task WrapAdminEventTask(Func<Task> action)
    {
        Task.Run(() => { action(); });

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
}
