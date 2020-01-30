using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Controllers.Accounts {
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
