using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class DiscordCodeController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IEventBus<Account> _accountEventBus;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;

        public DiscordCodeController(
            IAccountContext accountContext,
            IConfirmationCodeService confirmationCodeService,
            IHttpContextService httpContextService,
            IEventBus<Account> accountEventBus,
            ILogger logger
        ) {
            _accountContext = accountContext;
            _confirmationCodeService = confirmationCodeService;
            _httpContextService = httpContextService;
            _accountEventBus = accountEventBus;
            _logger = logger;
        }

        [HttpPost("{discordId}"), Authorize]
        public async Task<IActionResult> DiscordConnect(string discordId, [FromBody] JObject body) {
            string value = await _confirmationCodeService.GetConfirmationCode(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != discordId) {
                return BadRequest(new { error = "Code was invalid or expired. Please try again" });
            }

            string id = _httpContextService.GetUserId();
            await _accountContext.Update(id, Builders<Account>.Update.Set(x => x.DiscordId, discordId));
            Account account = _accountContext.GetSingle(id);
            _accountEventBus.Send(account);
            _logger.LogAudit($"DiscordID updated for {account.Id} to {discordId}");
            return Ok();
        }
    }
}
