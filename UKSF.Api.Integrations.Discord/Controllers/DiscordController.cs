using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base.Events;
using UKSF.Api.Discord.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Discord.Controllers {
    [Route("[controller]")]
    public class DiscordController : Controller {
        private readonly IDiscordService _discordService;
        private readonly IEventBus _eventBus;

        public DiscordController(IDiscordService discordService, IEventBus eventBus) {
            _discordService = discordService;
            _eventBus = eventBus;
        }

        [HttpGet("roles"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> GetRoles() {
            IReadOnlyCollection<SocketRole> roles = await _discordService.GetRoles();
            return Ok(roles.OrderBy(x => x.Name).Select(x => $"{x.Name}: {x.Id}").Aggregate((x, y) => $"{x}\n{y}"));
        }

        [HttpGet("updateuserroles"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> UpdateUserRoles() {
            await _discordService.UpdateAllUsers();
            return Ok();
        }

        [HttpGet("{accountId}/onlineUserDetails"), Authorize, Permissions(Permissions.RECRUITER)]
        public OnlineState GetOnlineUserDetails([FromRoute] string accountId) => _discordService.GetOnlineUserDetails(accountId);
    }
}
