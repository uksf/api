using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class DiscordCodeController : Controller {
        private readonly IEventBus<Account> _accountEventBus;
        private readonly IAccountService _accountService;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;

        public DiscordCodeController(
            IConfirmationCodeService confirmationCodeService,
            IAccountService accountService,
            IHttpContextService httpContextService,
            IEventBus<Account> accountEventBus,
            ILogger logger
        ) {
            _confirmationCodeService = confirmationCodeService;
            _accountService = accountService;
            _httpContextService = httpContextService;
            _accountEventBus = accountEventBus;
            _logger = logger;
        }

        // TODO: Could use an account data update event handler
        [HttpPost("{discordId}"), Authorize]
        public async Task<IActionResult> DiscordConnect(string discordId, [FromBody] JObject body) {
            string value = await _confirmationCodeService.GetConfirmationCode(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != discordId) {
                return BadRequest(new { error = "Code was invalid or expired. Please try again" });
            }

            string id = _httpContextService.GetUserId();
            await _accountService.Data.Update(id, Builders<Account>.Update.Set(x => x.discordId, discordId));
            Account account = _accountService.Data.GetSingle(id);
            _accountEventBus.Send(account);
            _logger.LogAudit($"DiscordID updated for {account.id} to {discordId}");
            return Ok();
        }
    }
}
