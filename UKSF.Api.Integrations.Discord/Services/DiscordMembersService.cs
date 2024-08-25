using Discord;
using Discord.WebSocket;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Integrations.Discord.Services;

public interface IDiscordMembersService : IDiscordService
{
    Task<IReadOnlyCollection<SocketRole>> GetRoles();
    OnlineState GetOnlineUserDetails(string accountId);
    Task UpdateAllUsers();
    Task UpdateUserByAccount(DomainAccount account);
    Task UpdateUserById(ulong discordId);
}

public class DiscordMembersService(
    IDiscordClientService discordClientService,
    IHttpContextService httpContextService,
    IVariablesService variablesService,
    IAccountContext accountContext,
    IUnitsContext unitsContext,
    IRanksContext ranksContext,
    IUnitsService unitsService,
    IDisplayNameService displayNameService,
    IUksfLogger logger
) : DiscordBaseService(discordClientService, accountContext, httpContextService, variablesService, logger), IDiscordMembersService
{
    private readonly IVariablesService _variablesService = variablesService;
    private readonly IAccountContext _accountContext = accountContext;
    private readonly IUksfLogger _logger = logger;

    public void Activate()
    {
        var client = GetClient();
        client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
        client.UserJoined += ClientOnUserJoined;
    }

    public Task CreateCommands()
    {
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<SocketRole>> GetRoles()
    {
        await AssertOnline();

        var guild = GetGuild();
        return guild.Roles;
    }

    public OnlineState GetOnlineUserDetails(string accountId)
    {
        var account = _accountContext.GetSingle(accountId);
        if (account?.DiscordId == null || !ulong.TryParse(account.DiscordId, out var discordId))
        {
            return null;
        }

        var online = IsAccountOnline(discordId);
        var nickname = GetAccountNickname(discordId);

        return new OnlineState { Online = online, Nickname = nickname };
    }

    public async Task UpdateAllUsers()
    {
        if (IsDiscordDisabled())
        {
            return;
        }

        await AssertOnline();
        var guild = GetGuild();
        await Task.Run(
            () =>
            {
                foreach (var user in guild.Users)
                {
                    _ = UpdateUserById(user.Id);
                }
            }
        );
    }

    public async Task UpdateUserByAccount(DomainAccount account)
    {
        if (IsDiscordDisabled())
        {
            return;
        }

        await AssertOnline();

        if (account is null)
        {
            _logger.LogError("Tried to update Discord for an unspecified account");
            return;
        }

        if (account.DiscordId is null)
        {
            _logger.LogWarning($"Tried to update Discord for account {account.Id}, but no DiscordId is linked");
            return;
        }

        if (!ulong.TryParse(account.DiscordId, out var discordId))
        {
            _logger.LogError($"Tried to update Discord for account {account.Id}, but DiscordId {account.DiscordId} did not parse correctly");
            return;
        }

        if (_variablesService.GetVariable("DID_U_BLACKLIST").AsArray().Contains(discordId.ToString()))
        {
            return;
        }

        var guild = GetGuild();
        var user = guild.GetUser(discordId);
        if (user == null)
        {
            _logger.LogWarning($"Tried to update Discord for account {{{account.Id}}}, but couldn't find user with DiscordId {discordId} in server");
            return;
        }

        await UpdateAccountRoles(user, account);
        await UpdateAccountNickname(user, account);
    }

    public async Task UpdateUserById(ulong discordId)
    {
        if (IsDiscordDisabled())
        {
            return;
        }

        await AssertOnline();

        if (discordId == 0)
        {
            _logger.LogError("Tried to update a Discord user without a DiscordID specified");
            return;
        }

        if (_variablesService.GetVariable("DID_U_BLACKLIST").AsArray().Contains(discordId.ToString()))
        {
            return;
        }

        var guild = GetGuild();
        var user = guild.GetUser(discordId);
        if (user == null)
        {
            _logger.LogWarning($"Tried to update a Discord user, but couldn't find user with DiscordId {discordId} in server");
            return;
        }

        var accounts = _accountContext.Get(x => x.DiscordId == discordId.ToString()).ToList();
        if (accounts.Count > 1)
        {
            _logger.LogError(
                $"Tried to update a Discord user with DiscordId {discordId}, but more than 1 account was found: {string.Join(",", accounts.Select(x => x.Id.EscapeForLogging()))}"
            );
            return;
        }

        var account = accounts.SingleOrDefault();
        if (account is null)
        {
            return;
        }

        await UpdateAccountRoles(user, account);
        await UpdateAccountNickname(user, account);
    }

    private async Task UpdateAccountRoles(SocketGuildUser user, DomainAccount account)
    {
        var userRoles = user.Roles;
        HashSet<string> allowedRoles = [];

        if (account is not null)
        {
            UpdateAccountRanks(account, allowedRoles);
            UpdateAccountUnits(account, allowedRoles);
        }

        var rolesBlacklist = _variablesService.GetVariable("DID_R_BLACKLIST").AsArray();
        foreach (var role in userRoles)
        {
            if (!allowedRoles.Contains(role.Id.ToString()) && !rolesBlacklist.Contains(role.Id.ToString()))
            {
                await user.RemoveRoleAsync(role);
            }
        }

        var roles = await GetRoles();
        foreach (var role in allowedRoles.Where(role => userRoles.All(x => x.Id.ToString() != role)))
        {
            if (ulong.TryParse(role, out var roleId))
            {
                await user.AddRoleAsync(roles.First(x => x.Id == roleId));
            }
        }
    }

    private async Task UpdateAccountNickname(IGuildUser user, DomainAccount account)
    {
        var name = account switch
        {
            null => user.DisplayName,
            _    => displayNameService.GetDisplayName(account)
        };

        if (user.Nickname != name)
        {
            try
            {
                await user.ModifyAsync(x => x.Nickname = name);
            }
            catch (Exception)
            {
                _logger.LogError(
                    $"Failed to update nickname for {(string.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname)}. Must manually be changed to: {name}"
                );
            }
        }
    }

    private void UpdateAccountRanks(DomainAccount account, HashSet<string> allowedRoles)
    {
        var rank = account.Rank;
        foreach (var x in ranksContext.Get().Where(x => rank == x.Name))
        {
            allowedRoles.Add(x.DiscordRoleId);
        }
    }

    private void UpdateAccountUnits(DomainAccount account, HashSet<string> allowedRoles)
    {
        var accountUnit = unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
        var accountUnits = unitsContext.Get(x => x.Members.Contains(account.Id)).Where(x => !string.IsNullOrEmpty(x.DiscordRoleId)).ToList();
        var accountUnitParents = unitsService.GetParents(accountUnit).Where(x => !string.IsNullOrEmpty(x.DiscordRoleId)).ToList();
        accountUnits.ForEach(x => allowedRoles.Add(x.DiscordRoleId));
        accountUnitParents.ForEach(x => allowedRoles.Add(x.DiscordRoleId));
    }

    private Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cachedOldUser, SocketGuildUser user)
    {
        return WrapEventTask(
            async () =>
            {
                var oldUser = await cachedOldUser.GetOrDownloadAsync();
                var oldRoles = oldUser.Roles.OrderBy(x => x.Id).Select(x => $"{x.Id}").Aggregate((x, y) => $"{x},{y}");
                var newRoles = user.Roles.OrderBy(x => x.Id).Select(x => $"{x.Id}").Aggregate((x, y) => $"{x},{y}");
                if (oldRoles != newRoles || oldUser.Nickname != user.Nickname)
                {
                    await UpdateUserById(user.Id);
                }
            }
        );
    }

    private Task ClientOnUserJoined(SocketGuildUser user)
    {
        return WrapEventTask(() => UpdateUserById(user.Id));
    }
}

