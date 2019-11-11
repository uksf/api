using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Services.Personnel;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class TeamspeakController : Controller {
        private readonly ITeamspeakService teamspeakService;

        public TeamspeakController(ITeamspeakService teamspeakService) => this.teamspeakService = teamspeakService;

        [HttpGet("online"), Authorize, Roles(RoleDefinitions.CONFIRMED, RoleDefinitions.MEMBER, RoleDefinitions.DISCHARGED)]
        public IActionResult GetOnlineClients() {
            object clients = teamspeakService.GetFormattedClients();
            return clients == null ? Ok(new { }) : Ok(new {clients});
        }

        [HttpGet("shutdown"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> Shutdown() {
            teamspeakService.Shutdown();
            await Task.Delay(TimeSpan.FromSeconds(3));
            return Ok();
        }
    }
}
