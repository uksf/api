using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class DiscordCodeController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IConfirmationCodeService _confirmationCodeService;
    private readonly IEventBus _eventBus;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;

    public DiscordCodeController(
        IAccountContext accountContext,
        IConfirmationCodeService confirmationCodeService,
        IHttpContextService httpContextService,
        IEventBus eventBus,
        IUksfLogger logger
    )
    {
        _accountContext = accountContext;
        _confirmationCodeService = confirmationCodeService;
        _httpContextService = httpContextService;
        _eventBus = eventBus;
        _logger = logger;
    }

    [HttpPost("{discordId}")]
    [Authorize]
    public async Task DiscordConnect([FromRoute] string discordId, [FromBody] DiscordCodeRequest discordCode)
    {
        var value = await _confirmationCodeService.GetConfirmationCodeValue(discordCode.Code);
        if (string.IsNullOrEmpty(value) || value != discordId)
        {
            throw new DiscordConnectFailedException();
        }

        var id = _httpContextService.GetUserId();
        await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.DiscordId, discordId));
        var domainAccount = _accountContext.GetSingle(id);
        _eventBus.Send(domainAccount);
        _logger.LogAudit($"DiscordID updated for {domainAccount.Id} to {discordId}");
    }
}
