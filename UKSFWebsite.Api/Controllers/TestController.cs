using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Signalr.Hubs.Integrations;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class TestController : Controller {
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly IHubContext<TeamspeakHub, ITeamspeakClient> hub;

        public TestController(IHubContext<TeamspeakHub, ITeamspeakClient> hub, IHostApplicationLifetime hostApplicationLifetime) {
            this.hub = hub;
            this.hostApplicationLifetime = hostApplicationLifetime;
        }

//        [HttpGet]
//        public async Task<IActionResult> Get() {
//            Task unused = Task.Run(
//                () => {
//                    Task.Delay(TimeSpan.FromSeconds(1));
//                    hostApplicationLifetime.StopApplication();
//                }
//            );
//            return Ok();
//        }
    }
}
