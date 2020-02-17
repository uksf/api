using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Controllers.Accounts {
    [Route("[controller]")]
    public class DiscordCodeController : Controller {
        private readonly IAccountService accountService;
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly IDiscordService discordService;
        private readonly ISessionService sessionService;

        public DiscordCodeController(ISessionService sessionService, IConfirmationCodeService confirmationCodeService, IAccountService accountService, IDiscordService discordService) {
            this.sessionService = sessionService;
            this.confirmationCodeService = confirmationCodeService;
            this.accountService = accountService;
            this.discordService = discordService;
        }

        [HttpPost("{discordId}"), Authorize]
        public async Task<IActionResult> DiscordConnect(string discordId, [FromBody] JObject body) {
            string value = await confirmationCodeService.GetConfirmationCode(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != discordId) {
                return BadRequest(new {error = "Code was invalid or expired. Please try again"});
            }

            string id = sessionService.GetContextId();
            await accountService.Data.Update(id, Builders<Account>.Update.Set(x => x.discordId, discordId));
            Account account = accountService.Data.GetSingle(id);
            await discordService.UpdateAccount(account);
            LogWrapper.AuditLog(account.id, $"DiscordID updated for {account.id} to {discordId}");
            return Ok();
        }
    }
}
