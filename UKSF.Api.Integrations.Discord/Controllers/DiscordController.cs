using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Integrations.Discord.Services;

namespace UKSF.Api.Integrations.Discord.Controllers;

[Route("[controller]")]
public class DiscordController : ControllerBase
{
    private readonly IDiscordMembersService _discordMembersService;

    public DiscordController(IDiscordMembersService discordMembersService)
    {
        _discordMembersService = discordMembersService;
    }

    [HttpGet("roles")]
    [Permissions(Permissions.Admin)]
    public async Task<string> GetRoles()
    {
        var roles = await _discordMembersService.GetRoles();
        return roles.OrderBy(x => x.Name).Select(x => $"{x.Name}: {x.Id}").Aggregate((x, y) => $"{x}\n{y}");
    }

    [HttpGet("updateuserroles")]
    [Permissions(Permissions.Admin)]
    public async Task UpdateUserRoles()
    {
        await _discordMembersService.UpdateAllUsers();
    }

    [HttpGet("{accountId}/onlineUserDetails")]
    [Permissions(Permissions.Recruiter)]
    public OnlineState GetOnlineUserDetails([FromRoute] string accountId)
    {
        return _discordMembersService.GetOnlineUserDetails(accountId);
    }
}
