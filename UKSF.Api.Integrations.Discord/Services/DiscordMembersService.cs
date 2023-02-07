using Discord;
using Discord.WebSocket;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Discord.Services;

public interface IDiscordMembersService
{
    Task<IReadOnlyCollection<SocketRole>> GetRoles();
    OnlineState GetOnlineUserDetails(string accountId);
    Task UpdateAllUsers();
    Task UpdateAccount(DomainAccount domainAccount, ulong discordId = 0);
}

public class DiscordMembersService : DiscordBaseService, IDiscordMembersService
{
    private readonly IAccountContext _accountContext;
    private readonly IDisplayNameService _displayNameService;
    private readonly IUksfLogger _logger;
    private readonly IRanksContext _ranksContext;
    private readonly IUnitsContext _unitsContext;
    private readonly IUnitsService _unitsService;
    private readonly IVariablesService _variablesService;

    public DiscordMembersService(
        IDiscordClientService discordClientService,
        IHttpContextService httpContextService,
        IVariablesService variablesService,
        IAccountContext accountContext,
        IUnitsContext unitsContext,
        IRanksContext ranksContext,
        IUnitsService unitsService,
        IDisplayNameService displayNameService,
        IUksfLogger logger
    ) : base(discordClientService, httpContextService, variablesService, logger)
    {
        _variablesService = variablesService;
        _accountContext = accountContext;
        _unitsContext = unitsContext;
        _ranksContext = ranksContext;
        _unitsService = unitsService;
        _displayNameService = displayNameService;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SocketRole>> GetRoles()
    {
        await AssertOnline();

        var guild = GetGuild();
        return guild.Roles;
    }

    public OnlineState GetOnlineUserDetails(string accountId)
    {
        var domainAccount = _accountContext.GetSingle(accountId);
        if (domainAccount?.DiscordId == null || !ulong.TryParse(domainAccount.DiscordId, out var discordId))
        {
            return null;
        }

        var online = IsAccountOnline(discordId);
        var nickname = GetAccountNickname(discordId);

        return new() { Online = online, Nickname = nickname };
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
                    var unused = UpdateAccount(null, user.Id);
                }
            }
        );
    }

    public async Task UpdateAccount(DomainAccount domainAccount, ulong discordId = 0)
    {
        if (IsDiscordDisabled())
        {
            return;
        }

        await AssertOnline();
        var guild = GetGuild();

        if (discordId == 0 && domainAccount != null && !string.IsNullOrEmpty(domainAccount.DiscordId))
        {
            discordId = ulong.Parse(domainAccount.DiscordId);
        }

        if (discordId != 0 && domainAccount == null)
        {
            domainAccount = _accountContext.GetSingle(x => !string.IsNullOrEmpty(x.DiscordId) && x.DiscordId == discordId.ToString());
        }

        if (discordId == 0)
        {
            return;
        }

        if (_variablesService.GetVariable("DID_U_BLACKLIST").AsArray().Contains(discordId.ToString()))
        {
            return;
        }

        var user = guild.GetUser(discordId);
        if (user == null)
        {
            return;
        }

        await UpdateAccountRoles(user, domainAccount);
        await UpdateAccountNickname(user, domainAccount);
    }

    public override void Activate()
    {
        var client = GetClient();
        client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
        client.UserJoined += ClientOnUserJoined;
    }

    private async Task UpdateAccountRoles(SocketGuildUser user, DomainAccount domainAccount)
    {
        var userRoles = user.Roles;
        HashSet<string> allowedRoles = new();

        if (domainAccount != null)
        {
            UpdateAccountRanks(domainAccount, allowedRoles);
            UpdateAccountUnits(domainAccount, allowedRoles);
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

    private async Task UpdateAccountNickname(IGuildUser user, DomainAccount domainAccount)
    {
        var name = _displayNameService.GetDisplayName(domainAccount);
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

    private void UpdateAccountRanks(DomainAccount domainAccount, ISet<string> allowedRoles)
    {
        var rank = domainAccount.Rank;
        foreach (var x in _ranksContext.Get().Where(x => rank == x.Name))
        {
            allowedRoles.Add(x.DiscordRoleId);
        }
    }

    private void UpdateAccountUnits(DomainAccount domainAccount, ISet<string> allowedRoles)
    {
        var accountUnit = _unitsContext.GetSingle(x => x.Name == domainAccount.UnitAssignment);
        var accountUnits = _unitsContext.Get(x => x.Members.Contains(domainAccount.Id)).Where(x => !string.IsNullOrEmpty(x.DiscordRoleId)).ToList();
        var accountUnitParents = _unitsService.GetParents(accountUnit).Where(x => !string.IsNullOrEmpty(x.DiscordRoleId)).ToList();
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
                    await UpdateAccount(null, user.Id);
                }
            }
        );
    }

    private Task ClientOnUserJoined(SocketGuildUser user)
    {
        return WrapEventTask(async () => { await UpdateAccount(null, user.Id); });
    }
}

