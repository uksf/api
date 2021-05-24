using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Discord.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Discord.Controllers
{
    [Route("[controller]")]
    public class DiscordController : ControllerBase
    {
        private readonly IDiscordService _discordService;

        public DiscordController(IDiscordService discordService)
        {
            _discordService = discordService;
        }

        [HttpGet("roles"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<string> GetRoles()
        {
            IReadOnlyCollection<SocketRole> roles = await _discordService.GetRoles();
            return roles.OrderBy(x => x.Name).Select(x => $"{x.Name}: {x.Id}").Aggregate((x, y) => $"{x}\n{y}");
        }

        [HttpGet("updateuserroles"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task UpdateUserRoles()
        {
            await _discordService.UpdateAllUsers();
        }

        [HttpGet("{accountId}/onlineUserDetails"), Authorize, Permissions(Permissions.RECRUITER)]
        public OnlineState GetOnlineUserDetails([FromRoute] string accountId)
        {
            return _discordService.GetOnlineUserDetails(accountId);
        }
    }
}
