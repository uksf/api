using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class DiscordController : Controller {
        private readonly IDiscordService discordService;

        public DiscordController(IDiscordService discordService) => this.discordService = discordService;

        [HttpGet("roles"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> GetRoles() {
            IReadOnlyCollection<SocketRole> roles = await discordService.GetRoles();
            return Ok(roles.OrderBy(x => x.Name).Select(x => $"{x.Name}: {x.Id}").Aggregate((x, y) => $"{x}\n{y}"));
        }

        [HttpGet("updateuserroles"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> UpdateUserRoles() {
            await discordService.UpdateAllUsers();
            return Ok();
        }
    }
}
