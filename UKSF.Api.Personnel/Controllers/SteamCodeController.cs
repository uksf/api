using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class SteamCodeController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;

        public SteamCodeController(IAccountContext accountContext, IConfirmationCodeService confirmationCodeService, IHttpContextService httpContextService, ILogger logger) {
            _accountContext = accountContext;
            _confirmationCodeService = confirmationCodeService;
            _httpContextService = httpContextService;
            _logger = logger;
        }

        [HttpPost("{steamId}"), Authorize]
        public async Task<IActionResult> SteamConnect(string steamId, [FromBody] JObject body) {
            string value = await _confirmationCodeService.GetConfirmationCode(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != steamId) {
                return BadRequest(new { error = "Code was invalid or expired. Please try again" });
            }

            string id = _httpContextService.GetUserId();
            await _accountContext.Update(id, "steamname", steamId);
            Account account = _accountContext.GetSingle(id);
            _logger.LogAudit($"SteamID updated for {account.Id} to {steamId}");
            return Ok();
        }
    }
}
