using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class SteamCodeController : Controller {
        private readonly IAccountService accountService;
        private readonly IHttpContextService httpContextService;
        private readonly ILogger logger;
        private readonly IConfirmationCodeService confirmationCodeService;


        public SteamCodeController(IConfirmationCodeService confirmationCodeService, IAccountService accountService, IHttpContextService httpContextService, ILogger logger) {

            this.confirmationCodeService = confirmationCodeService;
            this.accountService = accountService;
            this.httpContextService = httpContextService;
            this.logger = logger;
        }

        [HttpPost("{steamId}"), Authorize]
        public async Task<IActionResult> SteamConnect(string steamId, [FromBody] JObject body) {
            string value = await confirmationCodeService.GetConfirmationCode(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != steamId) {
                return BadRequest(new {error = "Code was invalid or expired. Please try again"});
            }

            string id = httpContextService.GetUserId();
            await accountService.Data.Update(id, "steamname", steamId);
            Account account = accountService.Data.GetSingle(id);
            logger.LogAudit($"SteamID updated for {account.id} to {steamId}");
            return Ok();
        }
    }
}
