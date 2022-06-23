using System.Linq;
using System.Threading.Tasks;
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

        [HttpGet("roles"), Permissions(Permissions.Admin)]
        public async Task<string> GetRoles()
        {
            var roles = await _discordService.GetRoles();
            return roles.OrderBy(x => x.Name).Select(x => $"{x.Name}: {x.Id}").Aggregate((x, y) => $"{x}\n{y}");
        }

        [HttpGet("updateuserroles"), Permissions(Permissions.Admin)]
        public async Task UpdateUserRoles()
        {
            await _discordService.UpdateAllUsers();
        }

        [HttpGet("{accountId}/onlineUserDetails"), Permissions(Permissions.Recruiter)]
        public OnlineState GetOnlineUserDetails([FromRoute] string accountId)
        {
            return _discordService.GetOnlineUserDetails(accountId);
        }
    }
}
