using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Exceptions;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("[controller]")]
    public class DiscordCodeController : ControllerBase
    {
        private readonly IAccountContext _accountContext;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly IEventBus _eventBus;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;

        public DiscordCodeController(IAccountContext accountContext, IConfirmationCodeService confirmationCodeService, IHttpContextService httpContextService, IEventBus eventBus, ILogger logger)
        {
            _accountContext = accountContext;
            _confirmationCodeService = confirmationCodeService;
            _httpContextService = httpContextService;
            _eventBus = eventBus;
            _logger = logger;
        }

        [HttpPost("{discordId}"), Authorize]
        public async Task DiscordConnect(string discordId, [FromBody] JObject body)
        {
            string value = await _confirmationCodeService.GetConfirmationCodeValue(body["code"].ToString());
            if (string.IsNullOrEmpty(value) || value != discordId)
            {
                throw new DiscordConnectFailedException();
            }

            string id = _httpContextService.GetUserId();
            await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.DiscordId, discordId));
            DomainAccount domainAccount = _accountContext.GetSingle(id);
            _eventBus.Send(domainAccount);
            _logger.LogAudit($"DiscordID updated for {domainAccount.Id} to {discordId}");
        }
    }
}
