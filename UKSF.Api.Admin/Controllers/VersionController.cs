using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Signalr.Clients;
using UKSF.Api.Admin.Signalr.Hubs;

namespace UKSF.Api.Admin.Controllers {
    [Route("[controller]")]
    public class VersionController : Controller {
        private readonly IHubContext<UtilityHub, IUtilityClient> _utilityHub;
        private readonly IVariablesContext _variablesContext;

        public VersionController(IVariablesContext variablesContext, IHubContext<UtilityHub, IUtilityClient> utilityHub) {
            _variablesContext = variablesContext;
            _utilityHub = utilityHub;
        }

        [HttpGet]
        public IActionResult GetFrontendVersion() => Ok(_variablesContext.GetSingle("FRONTEND_VERSION").AsString());

        [HttpPost("update"), Authorize]
        public async Task<IActionResult> UpdateFrontendVersion([FromBody] JObject body) {
            string version = body["version"].ToString();
            await _variablesContext.Update("FRONTEND_VERSION", version);
            await _utilityHub.Clients.All.ReceiveFrontendUpdate(version);
            return Ok();
        }
    }
}
