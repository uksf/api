using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Admin.SignalrHubs;
using UKSF.Api.Admin.SignalrHubs.Clients;
using UKSF.Api.Admin.SignalrHubs.Hubs;
using UKSF.Api.Interfaces.Hubs;

namespace UKSF.Api.Admin.Controllers {
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
