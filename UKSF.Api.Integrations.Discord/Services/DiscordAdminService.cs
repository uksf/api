using Discord;
using Discord.Rest;
using Discord.WebSocket;
using UKSF.Api.Discord.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Discord.Services;

public interface IDiscordAdminService { }

public class DiscordAdminService : DiscordBaseService, IDiscordAdminService
{
    private readonly IAccountContext _accountContext;
    private readonly IDisplayNameService _displayNameService;
    private readonly IEventBus _eventBus;
    private readonly IUksfLogger _logger;

    public DiscordAdminService(
        IDiscordClientService discordClientService,
        IHttpContextService httpContextService,
        IVariablesService variablesService,
        IAccountContext accountContext,
        IDisplayNameService displayNameService,
        IEventBus eventBus,
        IUksfLogger logger
    ) : base(discordClientService, httpContextService, variablesService, logger)
    {
        _accountContext = accountContext;
        _displayNameService = displayNameService;
        _eventBus = eventBus;
        _logger = logger;
    }

    public override void Activate()
    {
        var client = GetClient();
        client.UserJoined += ClientOnUserJoined;
        client.UserLeft += ClientOnUserLeft;
        client.UserBanned += ClientOnUserBanned;
        client.UserUnbanned += ClientOnUserUnbanned;
        client.MessageDeleted += ClientOnMessageDeleted;
        client.MessagesBulkDeleted += ClientOnMessagesBulkDeleted;
    }

    private Task ClientOnUserJoined(SocketGuildUser user)
    {
        return WrapAdminEventTask(
            () =>
            {
                var name = GetUserNickname(user);
                var connectedAccountMessage = GetConnectedAccountMessageFromUserId(user.Id);
                _logger.LogDiscordEvent(DiscordUserEventType.JOINED, user.Id.ToString(), name, string.Empty, name, $"Joined {connectedAccountMessage}");
                return Task.CompletedTask;
            }
        );
    }

    private Task ClientOnUserLeft(SocketGuild _, SocketUser user)
    {
        return WrapAdminEventTask(
            () =>
            {
                var domainAccount = _accountContext.GetSingle(x => x.DiscordId == user.Id.ToString());
                var connectedAccountMessage = GetConnectedAccountMessage(domainAccount);
                _logger.LogDiscordEvent(
                    DiscordUserEventType.LEFT,
                    user.Id.ToString(),
                    user.Username,
                    string.Empty,
                    user.Username,
                    $"Left {connectedAccountMessage}"
                );
                if (domainAccount != null)
                {
                    _eventBus.Send(new DiscordEventData(DiscordUserEventType.LEFT, domainAccount.Id));
                }

                return Task.CompletedTask;
            }
        );
    }

    private Task ClientOnUserBanned(SocketUser user, SocketGuild _)
    {
        return WrapAdminEventTask(
            async () =>
            {
                var connectedAccountMessage = GetConnectedAccountMessageFromUserId(user.Id);
                var instigatorId = await GetBannedAuditLogInstigator(user.Id);
                var guild = GetGuild();
                var instigatorName = GetUserNickname(guild.GetUser(instigatorId));
                _logger.LogDiscordEvent(
                    DiscordUserEventType.BANNED,
                    instigatorId.ToString(),
                    instigatorName,
                    string.Empty,
                    user.Username,
                    $"Banned {connectedAccountMessage}"
                );
            }
        );
    }

    private Task ClientOnUserUnbanned(SocketUser user, SocketGuild _)
    {
        return WrapAdminEventTask(
            async () =>
            {
                var connectedAccountMessage = GetConnectedAccountMessageFromUserId(user.Id);
                var instigatorId = await GetUnbannedAuditLogInstigator(user.Id);
                var guild = GetGuild();
                var instigatorName = GetUserNickname(guild.GetUser(instigatorId));
                _logger.LogDiscordEvent(
                    DiscordUserEventType.UNBANNED,
                    instigatorId.ToString(),
                    instigatorName,
                    string.Empty,
                    user.Username,
                    $"Unbanned {connectedAccountMessage}"
                );
            }
        );
    }

    private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> cachedChannel)
    {
        return WrapAdminEventTask(
            async () =>
            {
                var channel = await cachedChannel.GetOrDownloadAsync();
                var result = await GetDeletedMessageDetails(cacheable, channel);
                switch (result.InstigatorId)
                {
                    case ulong.MaxValue: return;
                    case 0:
                        _logger.LogDiscordEvent(
                            DiscordUserEventType.MESSAGE_DELETED,
                            "0",
                            "NO INSTIGATOR",
                            channel.Name,
                            string.Empty,
                            $"Irretrievable message {cacheable.Id} deleted"
                        );
                        return;
                    default:
                        _logger.LogDiscordEvent(
                            DiscordUserEventType.MESSAGE_DELETED,
                            result.InstigatorId.ToString(),
                            result.InstigatorName,
                            channel.Name,
                            result.Name,
                            result.Message
                        );
                        break;
                }
            }
        );
    }

    private Task ClientOnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> cacheables, Cacheable<IMessageChannel, ulong> cachedChannel)
    {
        return WrapAdminEventTask(
            async () =>
            {
                var irretrievableMessageCount = 0;
                List<DiscordDeletedMessageResult> messages = new();
                var channel = await cachedChannel.GetOrDownloadAsync();

                foreach (var cacheable in cacheables)
                {
                    var result = await GetDeletedMessageDetails(cacheable, channel);
                    switch (result.InstigatorId)
                    {
                        case ulong.MaxValue: continue;
                        case 0:
                            irretrievableMessageCount++;
                            continue;
                        default:
                            messages.Add(result);
                            break;
                    }
                }

                if (irretrievableMessageCount > 0)
                {
                    _logger.LogDiscordEvent(
                        DiscordUserEventType.MESSAGE_DELETED,
                        "0",
                        "NO INSTIGATOR",
                        channel.Name,
                        string.Empty,
                        $"{irretrievableMessageCount} irretrievable messages deleted"
                    );
                }

                var groupedMessages = messages.GroupBy(x => x.Name);
                foreach (var groupedMessage in groupedMessages)
                {
                    foreach (var result in groupedMessage)
                    {
                        _logger.LogDiscordEvent(
                            DiscordUserEventType.MESSAGE_DELETED,
                            result.InstigatorId.ToString(),
                            result.InstigatorName,
                            channel.Name,
                            result.Name,
                            result.Message
                        );
                    }
                }
            }
        );
    }

    private string GetConnectedAccountMessageFromUserId(ulong userId)
    {
        var domainAccount = _accountContext.GetSingle(x => x.DiscordId == userId.ToString());
        return GetConnectedAccountMessage(domainAccount);
    }

    private string GetConnectedAccountMessage(DomainAccount domainAccount)
    {
        return domainAccount == null
            ? "(No connected account)"
            : $"(Connected account - {domainAccount.Id}, {_displayNameService.GetDisplayName(domainAccount)}, {domainAccount.MembershipState.ToString()})";
    }

    private async Task<ulong> GetBannedAuditLogInstigator(ulong userId)
    {
        var guild = GetGuild();
        var auditLogsEnumerator = guild.GetAuditLogsAsync(10, RequestOptions.Default, null, null, ActionType.Ban).GetAsyncEnumerator();
        try
        {
            while (await auditLogsEnumerator.MoveNextAsync())
            {
                var auditLogs = auditLogsEnumerator.Current;
                var auditUser = auditLogs.Where(x => x.Data is BanAuditLogData)
                                         .Select(x => new { Data = x.Data as BanAuditLogData, x.User })
                                         .FirstOrDefault(x => x.Data.Target.Id == userId);
                if (auditUser != null)
                {
                    return auditUser.User.Id;
                }
            }
        }
        finally
        {
            await auditLogsEnumerator.DisposeAsync();
        }

        return 0;
    }

    private async Task<ulong> GetUnbannedAuditLogInstigator(ulong userId)
    {
        var guild = GetGuild();
        var auditLogsEnumerator = guild.GetAuditLogsAsync(10, RequestOptions.Default, null, null, ActionType.Unban).GetAsyncEnumerator();
        try
        {
            while (await auditLogsEnumerator.MoveNextAsync())
            {
                var auditLogs = auditLogsEnumerator.Current;
                var auditUser = auditLogs.Where(x => x.Data is UnbanAuditLogData)
                                         .Select(x => new { Data = x.Data as UnbanAuditLogData, x.User })
                                         .FirstOrDefault(x => x.Data.Target.Id == userId);
                if (auditUser != null)
                {
                    return auditUser.User.Id;
                }
            }
        }
        finally
        {
            await auditLogsEnumerator.DisposeAsync();
        }

        return 0;
    }

    private async Task<DiscordDeletedMessageResult> GetDeletedMessageDetails(Cacheable<IMessage, ulong> cacheable, IMessageChannel channel)
    {
        var message = await cacheable.GetOrDownloadAsync();
        if (message == null)
        {
            return new(0, null, null, null);
        }

        var userId = message.Author.Id;
        var instigatorId = await GetMessageDeletedAuditLogInstigator(channel.Id, userId);
        if (instigatorId == 0 || instigatorId == userId)
        {
            return new(ulong.MaxValue, null, null, null);
        }

        var guild = GetGuild();
        var name = message.Author is SocketGuildUser user ? GetUserNickname(user) : GetUserNickname(guild.GetUser(userId));
        var instigatorName = GetUserNickname(guild.GetUser(instigatorId));
        var messageString = message.Content;

        return new(instigatorId, instigatorName, name, messageString);
    }

    private async Task<ulong> GetMessageDeletedAuditLogInstigator(ulong channelId, ulong authorId)
    {
        var guild = GetGuild();
        var auditLogsEnumerator = guild.GetAuditLogsAsync(10, RequestOptions.Default, null, null, ActionType.MessageDeleted).GetAsyncEnumerator();
        try
        {
            while (await auditLogsEnumerator.MoveNextAsync())
            {
                var auditLogs = auditLogsEnumerator.Current;
                var auditUser = auditLogs.Where(x => x.Data is MessageDeleteAuditLogData)
                                         .Select(x => new { Data = x.Data as MessageDeleteAuditLogData, x.User })
                                         .FirstOrDefault(x => x.Data.ChannelId == channelId && x.Data.Target.Id == authorId);
                if (auditUser != null)
                {
                    return auditUser.User.Id;
                }
            }
        }
        finally
        {
            await auditLogsEnumerator.DisposeAsync();
        }

        return 0;
    }
}
