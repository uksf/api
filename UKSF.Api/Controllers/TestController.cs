using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Signalr.Hubs.Integrations;

namespace UKSF.Api.Controllers {
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
