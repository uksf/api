using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services {
    public class DiscordService : IDiscordService, IDisposable {
        private static readonly string[] REPLIES = {"Why thank you {0}", "Thank you {0}, you're too kind", "Thank you so much {0}", "Aw shucks {0} you're embarrassing me"};
        private static readonly string[] TRIGGERS = {"thank you", "thank", "best", "mvp", "love you", "appreciate you", "good"};
        private readonly IAccountService accountService;
        private readonly IConfiguration configuration;
        private readonly IDisplayNameService displayNameService;
        private readonly IRanksService ranksService;
        private readonly ulong specialUser;
        private readonly IUnitsService unitsService;
        private DiscordSocketClient client;
        private bool connected;
        private SocketGuild guild;
        private IReadOnlyCollection<SocketRole> roles;

        public DiscordService(IConfiguration configuration, IRanksService ranksService, IUnitsService unitsService, IAccountService accountService, IDisplayNameService displayNameService) {
            this.configuration = configuration;
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.accountService = accountService;
            this.displayNameService = displayNameService;
            specialUser = VariablesWrapper.VariablesService().GetSingle("DID_U_MASTER").AsUlong();
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
                account = accountService.GetSingle(x => !string.IsNullOrEmpty(x.discordId) && x.discordId == discordId.ToString());
            }

            if (discordId == 0) return;
            if (VariablesWrapper.VariablesService().GetSingle("DID_U_BLACKLIST").AsArray().Contains(discordId.ToString())) return;

            SocketGuildUser user = guild.GetUser(discordId);
            if (user == null) return;
            await UpdateAccountRoles(user, account);
            await UpdateAccountNickname(user, account);
        }

        public bool IsAccountOnline(Account account) => account.discordId != null && guild.GetUser(ulong.Parse(account.discordId))?.Status == UserStatus.Online;

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

            string[] rolesBlacklist = VariablesWrapper.VariablesService().GetSingle("DID_R_BLACKLIST").AsArray();
            foreach (SocketRole role in userRoles) {
                if (!allowedRoles.Contains(role.Id.ToString()) && !rolesBlacklist.Contains(role.Id.ToString())) {
                    await user.RemoveRoleAsync(role);
                }
            }

            foreach (string role in allowedRoles) {
                if (userRoles.All(x => x.Id.ToString() != role)) {
                    if (ulong.TryParse(role, out ulong roleId)) {
                        await user.AddRoleAsync(roles.First(x => x.Id == roleId));
                    }
                }
            }
        }

        private async Task UpdateAccountNickname(IGuildUser user, Account account) {
            string name = displayNameService.GetDisplayName(account);
            if (user.Nickname != name) {
                try {
                    await user.ModifyAsync(x => x.Nickname = name);
                } catch (Exception) {
                    LogWrapper.Log($"Failed to update nickname for {user.Nickname}. Must manually be changed to: {name}");
                }
            }
        }

        private void UpdateAccountRanks(Account account, ISet<string> allowedRoles) {
            string rank = account.rank;
            ranksService.Get()
                        .ForEach(
                            x => {
                                if (rank == x.name) {
                                    allowedRoles.Add(x.discordRoleId);
                                }
                            }
                        );
        }

        private void UpdateAccountUnits(Account account, ISet<string> allowedRoles) {
            Unit accountUnit = unitsService.GetSingle(x => x.name == account.unitAssignment);
            List<Unit> accountUnits = unitsService.Get(x => x.members.Contains(account.id)).Where(x => !string.IsNullOrEmpty(x.discordRoleId)).ToList();
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
                    string message = REPLIES[new Random().Next(0, REPLIES.Length)];
                    string[] parts = guild.GetUser(incomingMessage.Author.Id).Nickname.Split('.');
                    string nickname = incomingMessage.Author.Id == specialUser ? "Master" :
                        parts.Length > 1 ? parts[1] : parts[0];
                    await SendMessage(incomingMessage.Channel.Id, string.Format(message, nickname));
                }
            }
        }

        private Task OnClientOnReady() {
            guild = client.GetGuild(VariablesWrapper.VariablesService().GetSingle("DID_SERVER").AsUlong());
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
