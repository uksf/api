using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Integrations.Discord.Services;

namespace UKSF.Api.Integrations.Discord.Controllers;

[Route("[controller]")]
public class DiscordController(IDiscordMembersService discordMembersService, IDiscordGithubService discordGithubService) : ControllerBase
{
    [HttpGet("roles")]
    [Permissions(Permissions.Admin)]
    public async Task<string> GetRoles()
    {
        var roles = await discordMembersService.GetRoles();
        return roles.OrderBy(x => x.Name).Select(x => $"{x.Name}: {x.Id}").Aggregate((x, y) => $"{x}\n{y}");
    }

    [HttpGet("updateuserroles")]
    [Permissions(Permissions.Admin)]
    public async Task UpdateUserRoles()
    {
        await discordMembersService.UpdateAllUsers();
    }

    [HttpGet("{accountId}/onlineUserDetails")]
    [Permissions(Permissions.Recruiter)]
    public OnlineState GetOnlineUserDetails([FromRoute] string accountId)
    {
        return discordMembersService.GetOnlineUserDetails(accountId);
    }

    [HttpDelete("commands/newGithubIssue")]
    [Permissions(Permissions.Admin)]
    public Task DeleteNewGithubIssueCommand()
    {
        return discordGithubService.DeleteCommands();
    }
}
