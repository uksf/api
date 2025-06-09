using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Integrations.Discord.Services;

public interface IDiscordMessageService : IDiscordService
{
    Task SendMessageToEveryone(ulong channelId, string message, bool notifyOffline);
    Task SendMessage(ulong channelId, string message);
}

public class DiscordMessageService(
    IDiscordClientService discordClientService,
    IAccountContext accountContext,
    IHttpContextService httpContextService,
    IVariablesService variablesService,
    IUksfLogger logger
) : DiscordBaseService(discordClientService, accountContext, httpContextService, variablesService, logger), IDiscordMessageService
{
    private static readonly string[] OwnerReplies =
    [
        "Why thank you {0} owo", "Thank you {0}, you're too kind", "Thank you so much {0} uwu", "Aw shucks {0} you're embarrassing me"
    ];

    private static readonly string[] Replies =
    [
        "Why thank you {0}", "Thank you {0}, you're too kind", "Thank you so much {0}", "Aw shucks {0} you're embarrassing me"
    ];

    private static readonly string[] Triggers = ["thank you", "thank", "best", "mvp", "love you", "appreciate you", "good"];

    private static readonly List<Emote> Emotes =
    [
        Emote.Parse("<:Tuesday:732349730809708564>"),
        Emote.Parse("<:Thursday:732349755816149062>"),
        Emote.Parse("<:Friday:732349765060395029>"),
        Emote.Parse("<:Sunday:732349782541991957>")
    ];

    private readonly IDiscordClientService _discordClientService = discordClientService;
    private readonly IVariablesService _variablesService = variablesService;

    public void Activate()
    {
        var client = GetClient();
        client.MessageReceived += ClientOnMessageReceived;
        client.ReactionAdded += ClientOnReactionAdded;
        client.ReactionRemoved += ClientOnReactionRemoved;
    }

    public Task CreateCommands()
    {
        return Task.CompletedTask;
    }

    public async Task SendMessage(ulong channelId, string message)
    {
        if (_discordClientService.IsDiscordDisabled())
        {
            return;
        }

        await _discordClientService.AssertOnline();

        var guild = _discordClientService.GetGuild();
        var channel = guild.GetTextChannel(channelId);
        await channel.SendMessageAsync(message);
    }

    public async Task SendMessageToEveryone(ulong channelId, string message, bool notifyOffline)
    {
        if (_discordClientService.IsDiscordDisabled())
        {
            return;
        }

        var guild = _discordClientService.GetGuild();
        var tag = notifyOffline ? guild.EveryoneRole.ToString() : "@here";

        await SendMessage(channelId, $"{tag}\n{message}");
    }

    private Task ClientOnMessageReceived(SocketMessage message)
    {
        return WrapEventTask(async () =>
            {
                if (MessageIsWeeklyEventsMessage(message))
                {
                    await HandleWeeklyEventsMessageReacts(message);
                    return;
                }

                if (new Regex(@"\bbot\b", RegexOptions.IgnoreCase).IsMatch(message.Content) || message.MentionedUsers.Any(x => x.IsBot))
                {
                    await HandleBotMessageResponse(message);
                }
            }
        );
    }

    private Task ClientOnReactionAdded(Cacheable<IUserMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        return WrapEventTask(async () =>
            {
                var message = await cacheable.GetOrDownloadAsync();
                if (!MessageIsWeeklyEventsMessage(message))
                {
                    return;
                }

                if (!message.Reactions.TryGetValue(reaction.Emote, out var metadata))
                {
                    return;
                }

                if (!metadata.IsMe)
                {
                    return;
                }

                if (metadata.ReactionCount > 1)
                {
                    var client = GetClient();
                    await message.RemoveReactionAsync(reaction.Emote, client.CurrentUser);
                }
            }
        );
    }

    private Task ClientOnReactionRemoved(Cacheable<IUserMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        return WrapEventTask(async () =>
            {
                var message = await cacheable.GetOrDownloadAsync();
                if (!MessageIsWeeklyEventsMessage(message))
                {
                    return;
                }

                if (reaction.Emote is not Emote emote || Emotes.All(x => x.Id != emote.Id))
                {
                    return;
                }

                if (!message.Reactions.TryGetValue(reaction.Emote, out _))
                {
                    await message.AddReactionAsync(reaction.Emote);
                }
            }
        );
    }

    private bool MessageIsWeeklyEventsMessage(IMessage message)
    {
        if (message == null)
        {
            return false;
        }

        var weeklyEventsFilter = _variablesService.GetVariable("DISCORD_FILTER_WEEKLY_EVENTS").AsString();
        return message.Content.Contains(weeklyEventsFilter, StringComparison.InvariantCultureIgnoreCase);
    }

    private static async Task HandleWeeklyEventsMessageReacts(IMessage message)
    {
        foreach (var emote in Emotes)
        {
            await message.AddReactionAsync(emote);
        }
    }

    private async Task HandleBotMessageResponse(SocketMessage incomingMessage)
    {
        var guild = GetGuild();
        if (Triggers.Any(x => incomingMessage.Content.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
        {
            var owner = incomingMessage.Author.Id == _variablesService.GetVariable("DID_U_OWNER").AsUlong();
            var message = owner ? OwnerReplies[new Random().Next(0, OwnerReplies.Length)] : Replies[new Random().Next(0, Replies.Length)];
            var parts = guild.GetUser(incomingMessage.Author.Id).Nickname.Split('.');
            var nickname = owner ? "Daddy" :
                parts.Length > 1 ? parts[1] : parts[0];
            await SendMessage(incomingMessage.Channel.Id, string.Format(message, nickname));
        }
    }
}
