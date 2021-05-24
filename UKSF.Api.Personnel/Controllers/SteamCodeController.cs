using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Exceptions;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("[controller]")]
    public class SteamCodeController : ControllerBase
    {
        private readonly IAccountContext _accountContext;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;

        public SteamCodeController(IAccountContext accountContext, IConfirmationCodeService confirmationCodeService, IHttpContextService httpContextService, ILogger logger)
        {
            _accountContext = accountContext;
            _confirmationCodeService = confirmationCodeService;
            _httpContextService = httpContextService;
            _logger = logger;
        }

        [HttpPost("{steamId}"), Authorize]
        public async Task SteamConnect(string steamId, [FromBody] JObject body)
        {
            string value = await _confirmationCodeService.GetConfirmationCodeValue(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != steamId)
            {
                throw new InvalidConfirmationCodeException();
            }

            string id = _httpContextService.GetUserId();
            await _accountContext.Update(id, x => x.Steamname, steamId);
            DomainAccount domainAccount = _accountContext.GetSingle(id);
            _logger.LogAudit($"SteamID updated for {domainAccount.Id} to {steamId}");
        }
    }
}
