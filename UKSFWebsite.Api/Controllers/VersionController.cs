using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class VersionController : Controller {
        private readonly IVariablesService variablesService;
        private readonly IHubContext<UtilityHub, IUtilityClient> utilityHub;

        public VersionController(IVariablesService variablesService, IHubContext<UtilityHub, IUtilityClient> utilityHub) {
            this.variablesService = variablesService;
            this.utilityHub = utilityHub;
        }

        [HttpGet]
        public IActionResult GetFrontendVersion() => Ok(variablesService.GetSingle("FRONTEND_VERSION").AsString());

        [HttpPost("update"), Authorize]
        public async Task<IActionResult> UpdateFrontendVersion([FromBody] JObject body) {
            string version = body["version"].ToString();
            await variablesService.Update("FRONTEND_VERSION", version);
            await utilityHub.Clients.All.ReceiveFrontendUpdate(version);
            return Ok();
        }
    }
}
