using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

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
        private static readonly string[] OWNER_REPLIES = {"Why thank you {0} owo", "Thank you {0}, you're too kind", "Thank you so much {0} uwu", "Aw shucks {0} you're embarrassing me"};
        private static readonly string[] REPLIES = {"Why thank you {0}", "Thank you {0}, you're too kind", "Thank you so much {0}", "Aw shucks {0} you're embarrassing me"};
        private static readonly string[] TRIGGERS = {"thank you", "thank", "best", "mvp", "love you", "appreciate you", "good"};
        private readonly IAccountService accountService;
        private readonly IConfiguration configuration;
        private readonly IDisplayNameService displayNameService;
        private readonly IVariablesService variablesService;
        private readonly ILogger logger;
        private readonly IRanksService ranksService;
        private readonly ulong specialUser;
        private readonly IUnitsService unitsService;
        private DiscordSocketClient client;
        private bool connected;
        private SocketGuild guild;
        private IReadOnlyCollection<SocketRole> roles;

        public DiscordService(IConfiguration configuration, IRanksService ranksService, IUnitsService unitsService, IAccountService accountService, IDisplayNameService displayNameService, IVariablesService variablesService, ILogger logger) {
            this.configuration = configuration;
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.accountService = accountService;
            this.displayNameService = displayNameService;
            this.variablesService = variablesService;
            this.logger = logger;
            specialUser = variablesService.GetVariable("DID_U_OWNER").AsUlong();
        }

        public async Task ConnectDiscord() {
            if (client != null) {
                client.StopAsync().Wait(TimeSpan.FromSeconds(5));
                client = null;
            }

            client = new DiscordSocketClient();
            client.Ready += OnClientOnReady;
            client.Disconnected += ClientOnDisconnected;
            client.MessageReceived += ClientOnMessageReceived;
            client.UserJoined += ClientOnUserJoined;
            client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
            await client.LoginAsync(TokenType.Bot, configuration.GetConnectionString("discord"));
            await client.StartAsync();
        }

        public virtual async Task SendMessage(ulong channelId, string message) {
            await AssertOnline();

            SocketTextChannel channel = guild.GetTextChannel(channelId);
            await channel.SendMessageAsync(message);
        }

        public virtual async Task SendMessageToEveryone(ulong channelId, string message) {
            await SendMessage(channelId, $"{guild.EveryoneRole} {message}");
        }

        public async Task<IReadOnlyCollection<SocketRole>> GetRoles() {
            await AssertOnline();
            return roles;
        }

        public virtual async Task UpdateAllUsers() {
            await AssertOnline();
            await Task.Run(
                () => {
                    foreach (SocketGuildUser user in guild.Users) {
                        Task unused = UpdateAccount(null, user.Id);
                    }
                }
            );
        }

        public virtual async Task UpdateAccount(Account account, ulong discordId = 0) {
            await AssertOnline();
            if (discordId == 0 && account != null && !string.IsNullOrEmpty(account.discordId)) {
                discordId = ulong.Parse(account.discordId);
            }

            if (discordId != 0 && account == null) {
                account = accountService.Data.GetSingle(x => !string.IsNullOrEmpty(x.discordId) && x.discordId == discordId.ToString());
            }

            if (discordId == 0) return;
            if (variablesService.GetVariable("DID_U_BLACKLIST").AsArray().Contains(discordId.ToString())) return;

            SocketGuildUser user = guild.GetUser(discordId);
            if (user == null) return;
            await UpdateAccountRoles(user, account);
            await UpdateAccountNickname(user, account);
        }

        public (bool online, string nickname) GetOnlineUserDetails(Account account) {
            bool online = IsAccountOnline(account);
            string nickname = GetAccountNickname(account);

            return (online, nickname);
        }

        private bool IsAccountOnline(Account account) => account.discordId != null && guild.GetUser(ulong.Parse(account.discordId))?.Status == UserStatus.Online;

        private string GetAccountNickname(Account account) {
            if (account.discordId == null) return "";

            SocketGuildUser user = guild.GetUser(ulong.Parse(account.discordId));
            return user == null ? "" : string.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname;
        }

        public void Dispose() {
            client.StopAsync().Wait(TimeSpan.FromSeconds(5));
        }

        private async Task UpdateAccountRoles(SocketGuildUser user, Account account) {
            IReadOnlyCollection<SocketRole> userRoles = user.Roles;
            HashSet<string> allowedRoles = new HashSet<string>();

            if (account != null) {
                UpdateAccountRanks(account, allowedRoles);
                UpdateAccountUnits(account, allowedRoles);
            }

            string[] rolesBlacklist = variablesService.GetVariable("DID_R_BLACKLIST").AsArray();
            foreach (SocketRole role in userRoles) {
                if (!allowedRoles.Contains(role.Id.ToString()) && !rolesBlacklist.Contains(role.Id.ToString())) {
                    await user.RemoveRoleAsync(role);
                }
            }

            foreach (string role in allowedRoles.Where(role => userRoles.All(x => x.Id.ToString() != role))) {
                if (ulong.TryParse(role, out ulong roleId)) {
                    await user.AddRoleAsync(roles.First(x => x.Id == roleId));
                }
            }
        }

        private async Task UpdateAccountNickname(IGuildUser user, Account account) {
            string name = displayNameService.GetDisplayName(account);
            if (user.Nickname != name) {
                try {
                    await user.ModifyAsync(x => x.Nickname = name);
                } catch (Exception) {
                    logger.LogError($"Failed to update nickname for {(string.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname)}. Must manually be changed to: {name}");
                }
            }
        }

        private void UpdateAccountRanks(Account account, ISet<string> allowedRoles) {
            string rank = account.rank;
            foreach (Rank x in ranksService.Data.Get().Where(x => rank == x.name)) {
                allowedRoles.Add(x.discordRoleId);
            }
        }

        private void UpdateAccountUnits(Account account, ISet<string> allowedRoles) {
            Unit accountUnit = unitsService.Data.GetSingle(x => x.name == account.unitAssignment);
            List<Unit> accountUnits = unitsService.Data.Get(x => x.members.Contains(account.id)).Where(x => !string.IsNullOrEmpty(x.discordRoleId)).ToList();
            List<Unit> accountUnitParents = unitsService.GetParents(accountUnit).Where(x => !string.IsNullOrEmpty(x.discordRoleId)).ToList();
            accountUnits.ForEach(x => allowedRoles.Add(x.discordRoleId));
            accountUnitParents.ForEach(x => allowedRoles.Add(x.discordRoleId));
        }

        private async Task AssertOnline() {
            if (!connected) {
                await ConnectDiscord();
                while (!connected) {
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
                    bool owner = incomingMessage.Author.Id == specialUser;
                    string message = owner ? OWNER_REPLIES[new Random().Next(0, OWNER_REPLIES.Length)] : REPLIES[new Random().Next(0, REPLIES.Length)];
                    string[] parts = guild.GetUser(incomingMessage.Author.Id).Nickname.Split('.');
                    string nickname = owner ? "Daddy" : parts.Length > 1 ? parts[1] : parts[0];
                    await SendMessage(incomingMessage.Channel.Id, string.Format(message, nickname));
                }
            }
        }

        private Task OnClientOnReady() {
            guild = client.GetGuild(variablesService.GetVariable("DID_SERVER").AsUlong());
            roles = guild.Roles;
            connected = true;
            return null;
        }

        private Task ClientOnDisconnected(Exception arg) {
            connected = false;
            client.StopAsync().Wait(TimeSpan.FromSeconds(5));
            client = null;
            Task.Run(ConnectDiscord);
            return null;
        }
    }
}