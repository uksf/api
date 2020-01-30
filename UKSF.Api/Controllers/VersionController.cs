using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Services.Admin;
using UKSF.Api.Signalr.Hubs.Utility;

namespace UKSF.Api.Controllers {
    [Route("[controller]")]
    public class VersionController : Controller {
        private readonly IHubContext<UtilityHub, IUtilityClient> utilityHub;
        private readonly IVariablesDataService variablesDataService;

        public VersionController(IVariablesDataService variablesDataService, IHubContext<UtilityHub, IUtilityClient> utilityHub) {
            this.variablesDataService = variablesDataService;
            this.utilityHub = utilityHub;
        }

        [HttpGet]
        public IActionResult GetFrontendVersion() => Ok(variablesDataService.GetSingle("FRONTEND_VERSION").AsString());

        [HttpPost("update"), Authorize]
        public async Task<IActionResult> UpdateFrontendVersion([FromBody] JObject body) {
            string version = body["version"].ToString();
            await variablesDataService.Update("FRONTEND_VERSION", version);
            await utilityHub.Clients.All.ReceiveFrontendUpdate(version);
            return Ok();
        }
    }
}
