using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Message;

namespace UKSFWebsite.Api.Controllers.Accounts {
    [Route("[controller]")]
    public class SteamCodeController : Controller {
        private readonly IAccountService accountService;
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly ISessionService sessionService;

        public SteamCodeController(ISessionService sessionService, IConfirmationCodeService confirmationCodeService, IAccountService accountService) {
            this.sessionService = sessionService;
            this.confirmationCodeService = confirmationCodeService;
            this.accountService = accountService;
        }

        [HttpPost("{steamId}"), Authorize]
        public async Task<IActionResult> SteamConnect(string steamId, [FromBody] JObject body) {
            string value = await confirmationCodeService.GetConfirmationCode(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != steamId) {
                return BadRequest(new {error = "Code was invalid or expired. Please try again"});
            }

            string id = sessionService.GetContextId();
            await accountService.Data().Update(id, "steamname", steamId);
            Account account = accountService.Data().GetSingle(id);
            LogWrapper.AuditLog(account.id, $"SteamID updated for {account.id} to {steamId}");
            return Ok();
        }
    }
}
