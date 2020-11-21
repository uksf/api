using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Discord.Services {
    public interface IDiscordService {
        Task ConnectDiscord();
        (bool online, string nickname) GetOnlineUserDetails(Account account);
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
        private readonly ILogger _logger;
        private readonly IRanksContext _ranksContext;
        private readonly ulong _specialUser;
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
            ILogger logger
        ) {
            _unitsContext = unitsContext;
            _ranksContext = ranksContext;
            _accountContext = accountContext;
            _configuration = configuration;
            _unitsService = unitsService;
            _displayNameService = displayNameService;
            _variablesService = variablesService;
            _logger = logger;
            _specialUser = variablesService.GetVariable("DID_U_OWNER").AsUlong();
        }

        public async Task ConnectDiscord() {
            if (_client != null) {
                _client.StopAsync().Wait(TimeSpan.FromSeconds(5));
                _client = null;
            }

            _client = new DiscordSocketClient();
            _client.Ready += OnClientOnReady;
            _client.Disconnected += ClientOnDisconnected;
            _client.MessageReceived += ClientOnMessageReceived;
            _client.UserJoined += ClientOnUserJoined;
            _client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
            AddUserEventLogs();

            await _client.LoginAsync(TokenType.Bot, _configuration.GetConnectionString("discord"));
            await _client.StartAsync();
        }

        public async Task SendMessage(ulong channelId, string message) {
            await AssertOnline();

            SocketTextChannel channel = _guild.GetTextChannel(channelId);
            await channel.SendMessageAsync(message);
        }

        public async Task SendMessageToEveryone(ulong channelId, string message) {
            await SendMessage(channelId, $"{_guild.EveryoneRole} {message}");
        }

        public async Task<IReadOnlyCollection<SocketRole>> GetRoles() {
            await AssertOnline();
            return _roles;
        }

        public async Task UpdateAllUsers() {
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

        public (bool online, string nickname) GetOnlineUserDetails(Account account) {
            bool online = IsAccountOnline(account);
            string nickname = GetAccountNickname(account);

            return (online, nickname);
        }

        public void Dispose() {
            _client.StopAsync().Wait(TimeSpan.FromSeconds(5));
        }

        private bool IsAccountOnline(Account account) => account.DiscordId != null && _guild.GetUser(ulong.Parse(account.DiscordId))?.Status == UserStatus.Online;

        private string GetAccountNickname(Account account) {
            if (account.DiscordId == null) return "";

            SocketGuildUser user = _guild.GetUser(ulong.Parse(account.DiscordId));
            return GetUserNickname(user);
        }

        private static string GetUserNickname(SocketGuildUser user) => user == null ? "" : string.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname;

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
            string oldRoles = oldUser.Roles.OrderBy(x => x.Id).Select(x => $"{x.Id}").Aggregate((x, y) => $"{x},{y}");
            string newRoles = user.Roles.OrderBy(x => x.Id).Select(x => $"{x.Id}").Aggregate((x, y) => $"{x},{y}");
            if (oldRoles != newRoles || oldUser.Nickname != user.Nickname) {
                await UpdateAccount(null, user.Id);
            }
        }

        private async Task ClientOnUserJoined(SocketGuildUser user) {
            await UpdateAccount(null, user.Id);
        }

        private async Task ClientOnMessageReceived(SocketMessage incomingMessage) {
            if (incomingMessage.Content.Contains("bot", StringComparison.InvariantCultureIgnoreCase) || incomingMessage.MentionedUsers.Any(x => x.IsBot)) {
                if (TRIGGERS.Any(x => incomingMessage.Content.Contains(x, StringComparison.InvariantCultureIgnoreCase))) {
                    bool owner = incomingMessage.Author.Id == _specialUser;
                    string message = owner ? OWNER_REPLIES[new Random().Next(0, OWNER_REPLIES.Length)] : REPLIES[new Random().Next(0, REPLIES.Length)];
                    string[] parts = _guild.GetUser(incomingMessage.Author.Id).Nickname.Split('.');
                    string nickname = owner ? "Daddy" : parts.Length > 1 ? parts[1] : parts[0];
                    await SendMessage(incomingMessage.Channel.Id, string.Format(message, nickname));
                }
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
                _logger.LogDiscordEvent(DiscordUserEventType.JOINED, name, user.Id.ToString(), $"{name} joined");
                return Task.CompletedTask;
            };

            _client.UserLeft += user => {
                string name = GetUserNickname(user);
                _logger.LogDiscordEvent(DiscordUserEventType.LEFT, name, user.Id.ToString(), $"{name} left");
                return Task.CompletedTask;
            };

            _client.UserBanned += (user, _) => {
                _logger.LogDiscordEvent(DiscordUserEventType.BANNED, user.Username, user.Id.ToString(), $"{user.Username} banned");
                return Task.CompletedTask;
            };

            _client.UserUnbanned += (user, _) => {
                _logger.LogDiscordEvent(DiscordUserEventType.UNBANNED, user.Username, user.Id.ToString(), $"{user.Username} unbanned");
                return Task.CompletedTask;
            };
        }
    }
}
