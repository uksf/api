using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class DiscordCodeController : Controller {
        private readonly IAccountService accountService;
        private readonly IHttpContextService httpContextService;
        private readonly IConfirmationCodeService confirmationCodeService;
        private readonly IDiscordService discordService;
        private readonly ILogger logger;

        public DiscordCodeController(IConfirmationCodeService confirmationCodeService, IAccountService accountService, IHttpContextService httpContextService, IDiscordService discordService, ILogger logger) {
            this.confirmationCodeService = confirmationCodeService;
            this.accountService = accountService;
            this.httpContextService = httpContextService;
            this.discordService = discordService;
            this.logger = logger;
        }

        [HttpPost("{discordId}"), Authorize]
        public async Task<IActionResult> DiscordConnect(string discordId, [FromBody] JObject body) {
            string value = await confirmationCodeService.GetConfirmationCode(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != discordId) {
                return BadRequest(new {error = "Code was invalid or expired. Please try again"});
            }

            string id = httpContextService.GetUserId();
            await accountService.Data.Update(id, Builders<Account>.Update.Set(x => x.discordId, discordId));
            Account account = accountService.Data.GetSingle(id);
            await discordService.UpdateAccount(account);
            logger.LogAudit($"DiscordID updated for {account.id} to {discordId}");
            return Ok();
        }
    }
}
