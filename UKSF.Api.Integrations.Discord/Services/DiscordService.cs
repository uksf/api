using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Discord.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Discord.Services {
    public interface IDiscordService {
        Task ConnectDiscord();
        OnlineState GetOnlineUserDetails(string accountId);
        Task SendMessageToEveryone(ulong channelId, string message);
        Task SendMessage(ulong channelId, string message);
        Task<IReadOnlyCollection<SocketRole>> GetRoles();
        Task UpdateAllUsers();
        Task UpdateAccount(Account account, ulong discordId = 0);
    }

    public class DiscordService : IDiscordService, IDisposable {
        private static readonly string[] OWNER_REPLIES = { "Why thank you {0} owo", "Thank you {0}, you're too kind", "Thank you so much {0} uwu", "Aw shucks {0} you're embarrassing me" };
        private static readonly string[] REPLIES = { "Why thank you {0}", "Thank you {0}, you're too kind", "Thank you so much {0}", "Aw shucks {0} you're embarrassing me" };
        private static readonly string[] TRIGGERS = { "thank you", "thank", "best", "mvp", "love you", "appreciate you", "good" };
        private readonly IAccountContext _accountContext;
        private readonly IConfiguration _configuration;
        private readonly IDisplayNameService _displayNameService;
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;
        private readonly IRanksContext _ranksContext;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;
        private readonly IVariablesService _variablesService;
        private DiscordSocketClient _client;
        private bool _connected;
        private SocketGuild _guild;
        private IReadOnlyCollection<SocketRole> _roles;

        public DiscordService(
            IUnitsContext unitsContext,
            IRanksContext ranksContext,
            IAccountContext accountContext,
            IConfiguration configuration,
            IUnitsService unitsService,
            IDisplayNameService displayNameService,
            IVariablesService variablesService,
            ILogger logger,
            IEventBus eventBus
        ) {
            _unitsContext = unitsContext;
            _ranksContext = ranksContext;
            _accountContext = accountContext;
            _configuration = configuration;
            _unitsService = unitsService;
            _displayNameService = displayNameService;
            _variablesService = variablesService;
            _logger = logger;
            _eventBus = eventBus;
        }

        public async Task ConnectDiscord() {
            if (_client != null) {
                _client.StopAsync().Wait(TimeSpan.FromSeconds(5));
                _client = null;
            }

            _client = new DiscordSocketClient(new DiscordSocketConfig { AlwaysDownloadUsers = true, MessageCacheSize = 1000 });
            _client.Ready += OnClientOnReady;
            _client.Disconnected += ClientOnDisconnected;
            _client.MessageReceived += ClientOnMessageReceived;
            _client.UserJoined += ClientOnUserJoined;
            _client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
            AddUserEventLogs();

            await _client.LoginAsync(TokenType.Bot, _configuration.GetConnectionString("discord"));
            await _client.StartAsync();

            await Task.Delay(TimeSpan.FromSeconds(5));
            await _guild.GetTextChannel(522851104128499712).SendMessageAsync(Guid.NewGuid().ToString());
        }

        public async Task SendMessage(ulong channelId, string message) {
            if (IsDiscordDisabled()) return;

            await AssertOnline();

            SocketTextChannel channel = _guild.GetTextChannel(channelId);
            await channel.SendMessageAsync(message);
        }

        public async Task SendMessageToEveryone(ulong channelId, string message) {
            if (IsDiscordDisabled()) return;

            await SendMessage(channelId, $"{_guild.EveryoneRole} {message}");
        }

        public async Task<IReadOnlyCollection<SocketRole>> GetRoles() {
            await AssertOnline();
            return _roles;
        }

        public async Task UpdateAllUsers() {
            if (IsDiscordDisabled()) return;

            await AssertOnline();
            await Task.Run(
                () => {
                    foreach (SocketGuildUser user in _guild.Users) {
                        Task unused = UpdateAccount(null, user.Id);
                    }
                }
            );
        }

        public async Task UpdateAccount(Account account, ulong discordId = 0) {
            if (IsDiscordDisabled()) return;

            await AssertOnline();
            if (discordId == 0 && account != null && !string.IsNullOrEmpty(account.DiscordId)) {
                discordId = ulong.Parse(account.DiscordId);
            }

            if (discordId != 0 && account == null) {
                account = _accountContext.GetSingle(x => !string.IsNullOrEmpty(x.DiscordId) && x.DiscordId == discordId.ToString());
            }

            if (discordId == 0) return;
            if (_variablesService.GetVariable("DID_U_BLACKLIST").AsArray().Contains(discordId.ToString())) return;

            SocketGuildUser user = _guild.GetUser(discordId);
            if (user == null) return;
            await UpdateAccountRoles(user, account);
            await UpdateAccountNickname(user, account);
        }

        // TODO: Change to use signalr if events are available
        public OnlineState GetOnlineUserDetails(string accountId) {
            Account account = _accountContext.GetSingle(accountId);
            if (account?.DiscordId == null || !ulong.TryParse(account.DiscordId, out ulong discordId)) {
                return null;
            }

            bool online = IsAccountOnline(discordId);
            string nickname = GetAccountNickname(discordId);

            return new OnlineState { Online = online, Nickname = nickname };
        }

        public void Dispose() {
            _client?.StopAsync().Wait(TimeSpan.FromSeconds(5));
        }

        private bool IsDiscordDisabled() => !_variablesService.GetFeatureState("DISCORD");

        private bool IsAccountOnline(ulong discordId) => _guild.GetUser(discordId)?.Status == UserStatus.Online;

        private string GetAccountNickname(ulong discordId) {
            SocketGuildUser user = _guild.GetUser(discordId);
            return GetUserNickname(user);
        }

        private static string GetUserNickname(IGuildUser user) => user == null ? "" : string.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname;

        private async Task UpdateAccountRoles(SocketGuildUser user, Account account) {
            IReadOnlyCollection<SocketRole> userRoles = user.Roles;
            HashSet<string> allowedRoles = new();

            if (account != null) {
                UpdateAccountRanks(account, allowedRoles);
                UpdateAccountUnits(account, allowedRoles);
            }

            string[] rolesBlacklist = _variablesService.GetVariable("DID_R_BLACKLIST").AsArray();
            foreach (SocketRole role in userRoles) {
                if (!allowedRoles.Contains(role.Id.ToString()) && !rolesBlacklist.Contains(role.Id.ToString())) {
                    await user.RemoveRoleAsync(role);
                }
            }

            foreach (string role in allowedRoles.Where(role => userRoles.All(x => x.Id.ToString() != role))) {
                if (ulong.TryParse(role, out ulong roleId)) {
                    await user.AddRoleAsync(_roles.First(x => x.Id == roleId));
                }
            }
        }

        private async Task UpdateAccountNickname(IGuildUser user, Account account) {
            string name = _displayNameService.GetDisplayName(account);
            if (user.Nickname != name) {
                try {
                    await user.ModifyAsync(x => x.Nickname = name);
                } catch (Exception) {
                    _logger.LogError($"Failed to update nickname for {(string.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname)}. Must manually be changed to: {name}");
                }
            }
        }

        private void UpdateAccountRanks(Account account, ISet<string> allowedRoles) {
            string rank = account.Rank;
            foreach (Rank x in _ranksContext.Get().Where(x => rank == x.Name)) {
                allowedRoles.Add(x.DiscordRoleId);
            }
        }

        private void UpdateAccountUnits(Account account, ISet<string> allowedRoles) {
            Unit accountUnit = _unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
            List<Unit> accountUnits = _unitsContext.Get(x => x.Members.Contains(account.Id)).Where(x => !string.IsNullOrEmpty(x.DiscordRoleId)).ToList();
            List<Unit> accountUnitParents = _unitsService.GetParents(accountUnit).Where(x => !string.IsNullOrEmpty(x.DiscordRoleId)).ToList();
            accountUnits.ForEach(x => allowedRoles.Add(x.DiscordRoleId));
            accountUnitParents.ForEach(x => allowedRoles.Add(x.DiscordRoleId));
        }

        private async Task AssertOnline() {
            if (!_connected) {
                await ConnectDiscord();
                while (!_connected) {
                    await Task.Delay(50);
                }
            }
        }

        private async Task ClientOnGuildMemberUpdated(SocketGuildUser oldUser, SocketGuildUser user) {
            if (IsDiscordDisabled()) return;

            string oldRoles = oldUser.Roles.OrderBy(x => x.Id).Select(x => $"{x.Id}").Aggregate((x, y) => $"{x},{y}");
            string newRoles = user.Roles.OrderBy(x => x.Id).Select(x => $"{x.Id}").Aggregate((x, y) => $"{x},{y}");
            if (oldRoles != newRoles || oldUser.Nickname != user.Nickname) {
                await UpdateAccount(null, user.Id);
            }
        }

        private async Task ClientOnUserJoined(SocketGuildUser user) {
            if (IsDiscordDisabled()) return;

            await UpdateAccount(null, user.Id);
        }

        private async Task ClientOnMessageReceived(SocketMessage incomingMessage) {
            if (IsDiscordDisabled()) return;

            if (incomingMessage.Content.Contains(_variablesService.GetVariable("DISCORD_FILTER_WEEKLY_EVENTS").AsString(), StringComparison.InvariantCultureIgnoreCase)) {
                await HandleWeeklyEventsMessageReacts(incomingMessage);
                return;
            }

            if (incomingMessage.Content.Contains("bot", StringComparison.InvariantCultureIgnoreCase) || incomingMessage.MentionedUsers.Any(x => x.IsBot)) {
                await HandleBotMessageResponse(incomingMessage);
            }
        }

        private static async Task HandleWeeklyEventsMessageReacts(IMessage incomingMessage) {
            List<string> reactionCodes = new() { ":Tuesday:", ":Thursday:", ":Friday:", ":Sunday:" };
            foreach (string reactionCode in reactionCodes) {
                await incomingMessage.AddReactionAsync(new Emoji(reactionCode));
            }
        }

        private async Task HandleBotMessageResponse(SocketMessage incomingMessage) {
            if (TRIGGERS.Any(x => incomingMessage.Content.Contains(x, StringComparison.InvariantCultureIgnoreCase))) {
                bool owner = incomingMessage.Author.Id == _variablesService.GetVariable("DID_U_OWNER").AsUlong();
                string message = owner ? OWNER_REPLIES[new Random().Next(0, OWNER_REPLIES.Length)] : REPLIES[new Random().Next(0, REPLIES.Length)];
                string[] parts = _guild.GetUser(incomingMessage.Author.Id).Nickname.Split('.');
                string nickname = owner ? "Daddy" : parts.Length > 1 ? parts[1] : parts[0];
                await SendMessage(incomingMessage.Channel.Id, string.Format(message, nickname));
            }
        }

        private Task OnClientOnReady() {
            _guild = _client.GetGuild(_variablesService.GetVariable("DID_SERVER").AsUlong());
            _roles = _guild.Roles;
            _connected = true;
            return Task.CompletedTask;
        }

        private Task ClientOnDisconnected(Exception arg) {
            _connected = false;
            _client.StopAsync().Wait(TimeSpan.FromSeconds(5));
            _client = null;
            Task.Run(ConnectDiscord);
            return Task.CompletedTask;
        }

        private void AddUserEventLogs() {
            _client.UserJoined += user => {
                string name = GetUserNickname(user);
                string associatedAccountMessage = GetAssociatedAccountMessage(user.Id);
                _logger.LogDiscordEvent(DiscordUserEventType.JOINED, user.Id.ToString(), name, string.Empty, name, $"Joined, {associatedAccountMessage}");
                return Task.CompletedTask;
            };

            _client.UserLeft += user => {
                string name = GetUserNickname(user);
                string associatedAccountMessage = GetAssociatedAccountMessage(user.Id);
                _logger.LogDiscordEvent(DiscordUserEventType.LEFT, user.Id.ToString(), name, string.Empty, name, $"Left, {associatedAccountMessage}");
                Account account = _accountContext.GetSingle(x => x.DiscordId == user.Id.ToString());
                if (account != null) {
                    _eventBus.Send(new DiscordEventData(DiscordUserEventType.LEFT, account.Id));
                }

                return Task.CompletedTask;
            };

            _client.UserBanned += async (user, _) => {
                string associatedAccountMessage = GetAssociatedAccountMessage(user.Id);
                ulong instigatorId = await GetBannedAuditLogInstigator(user.Id);
                string instigatorName = GetUserNickname(_guild.GetUser(instigatorId));
                _logger.LogDiscordEvent(DiscordUserEventType.BANNED, instigatorId.ToString(), instigatorName, string.Empty, user.Username, $"Banned, {associatedAccountMessage}");
            };

            _client.UserUnbanned += async (user, _) => {
                string associatedAccountMessage = GetAssociatedAccountMessage(user.Id);
                ulong instigatorId = await GetUnbannedAuditLogInstigator(user.Id);
                string instigatorName = GetUserNickname(_guild.GetUser(instigatorId));
                _logger.LogDiscordEvent(DiscordUserEventType.UNBANNED, instigatorId.ToString(), instigatorName, string.Empty, user.Username, $"Unbanned, {associatedAccountMessage}");
            };

            _client.MessagesBulkDeleted += async (cacheables, channel) => {
                int irretrievableMessageCount = 0;
                List<DiscordDeletedMessageResult> messages = new();

                foreach (Cacheable<IMessage, ulong> cacheable in cacheables) {
                    DiscordDeletedMessageResult result = await GetDeletedMessageDetails(cacheable, channel);
                    switch (result.InstigatorId) {
                        case ulong.MaxValue: continue;
                        case 0:
                            irretrievableMessageCount++;
                            continue;
                        default:
                            messages.Add(result);
                            break;
                    }
                }

                _logger.LogDiscordEvent(DiscordUserEventType.MESSAGE_DELETED, "0", "NO INSTIGATOR", channel.Name, string.Empty, $"{irretrievableMessageCount} irretrievable messages deleted");

                IEnumerable<IGrouping<string, DiscordDeletedMessageResult>> groupedMessages = messages.GroupBy(x => x.Name);
                foreach (IGrouping<string, DiscordDeletedMessageResult> groupedMessage in groupedMessages) {
                    foreach (DiscordDeletedMessageResult result in groupedMessage) {
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
            };

            _client.MessageDeleted += async (cacheable, channel) => {
                DiscordDeletedMessageResult result = await GetDeletedMessageDetails(cacheable, channel);
                switch (result.InstigatorId) {
                    case ulong.MaxValue: return;
                    case 0:
                        _logger.LogDiscordEvent(DiscordUserEventType.MESSAGE_DELETED, "0", "NO INSTIGATOR", channel.Name, string.Empty, $"Irretrievable message {cacheable.Id} deleted");
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
            };
        }

        private string GetAssociatedAccountMessage(ulong userId) {
            Account account = _accountContext.GetSingle(x => x.DiscordId == userId.ToString());
            return account == null ? "with no associated account" : $"with associated account ({account.Id}, {_displayNameService.GetDisplayName(account)})";
        }

        private async Task<DiscordDeletedMessageResult> GetDeletedMessageDetails(Cacheable<IMessage, ulong> cacheable, ISocketMessageChannel channel) {
            IMessage message = await cacheable.GetOrDownloadAsync();
            if (message == null) {
                return new DiscordDeletedMessageResult(0, null, null, null);
            }

            ulong userId = message.Author.Id;
            ulong instigatorId = await GetMessageDeletedAuditLogInstigator(channel.Id, userId);
            if (instigatorId == 0 || instigatorId == userId) {
                return new DiscordDeletedMessageResult(ulong.MaxValue, null, null, null);
            }

            string name = message.Author is SocketGuildUser user ? GetUserNickname(user) : GetUserNickname(_guild.GetUser(userId));
            string instigatorName = GetUserNickname(_guild.GetUser(instigatorId));
            string messageString = message.Content;

            return new DiscordDeletedMessageResult(instigatorId, instigatorName, name, messageString);
        }

        private async Task<ulong> GetMessageDeletedAuditLogInstigator(ulong channelId, ulong authorId) {
            IAsyncEnumerator<IReadOnlyCollection<RestAuditLogEntry>> auditLogsEnumerator =
                _guild.GetAuditLogsAsync(10, RequestOptions.Default, null, null, ActionType.MessageDeleted).GetAsyncEnumerator();
            try {
                while (await auditLogsEnumerator.MoveNextAsync()) {
                    IReadOnlyCollection<RestAuditLogEntry> auditLogs = auditLogsEnumerator.Current;
                    var auditUser = auditLogs.Where(x => x.Data is MessageDeleteAuditLogData)
                                             .Select(x => new { Data = x.Data as MessageDeleteAuditLogData, x.User })
                                             .FirstOrDefault(x => x.Data.ChannelId == channelId && x.Data.AuthorId == authorId);
                    if (auditUser != null) return auditUser.User.Id;
                }
            } finally {
                await auditLogsEnumerator.DisposeAsync();
            }

            return 0;
        }

        private async Task<ulong> GetBannedAuditLogInstigator(ulong userId) {
            IAsyncEnumerator<IReadOnlyCollection<RestAuditLogEntry>> auditLogsEnumerator =
                _guild.GetAuditLogsAsync(10, RequestOptions.Default, null, null, ActionType.Ban).GetAsyncEnumerator();
            try {
                while (await auditLogsEnumerator.MoveNextAsync()) {
                    IReadOnlyCollection<RestAuditLogEntry> auditLogs = auditLogsEnumerator.Current;
                    var auditUser = auditLogs.Where(x => x.Data is BanAuditLogData)
                                             .Select(x => new { Data = x.Data as BanAuditLogData, x.User })
                                             .FirstOrDefault(x => x.Data.Target.Id == userId);
                    if (auditUser != null) return auditUser.User.Id;
                }
            } finally {
                await auditLogsEnumerator.DisposeAsync();
            }

            return 0;
        }

        private async Task<ulong> GetUnbannedAuditLogInstigator(ulong userId) {
            IAsyncEnumerator<IReadOnlyCollection<RestAuditLogEntry>> auditLogsEnumerator =
                _guild.GetAuditLogsAsync(10, RequestOptions.Default, null, null, ActionType.Unban).GetAsyncEnumerator();
            try {
                while (await auditLogsEnumerator.MoveNextAsync()) {
                    IReadOnlyCollection<RestAuditLogEntry> auditLogs = auditLogsEnumerator.Current;
                    var auditUser = auditLogs.Where(x => x.Data is UnbanAuditLogData)
                                             .Select(x => new { Data = x.Data as UnbanAuditLogData, x.User })
                                             .FirstOrDefault(x => x.Data.Target.Id == userId);
                    if (auditUser != null) return auditUser.User.Id;
                }
            } finally {
                await auditLogsEnumerator.DisposeAsync();
            }

            return 0;
        }
    }
}
