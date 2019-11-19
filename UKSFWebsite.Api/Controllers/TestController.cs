using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Signalr.Hubs.Integrations;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class TestController : Controller {
        private readonly IHubContext<TeamspeakHub, ITeamspeakClient> hub;

        public TestController(IHubContext<TeamspeakHub, ITeamspeakClient> hub) => this.hub = hub;

        [HttpGet]
        public async Task<IActionResult> Get() {
//            await hub.Clients.All.Receive("TEST");
            return Ok();
        }
    }
}
