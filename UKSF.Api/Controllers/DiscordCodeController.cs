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
        var otherAccounts = _accountContext.Get(x => x.Id != id && x.DiscordId == discordId).ToList();
        if (otherAccounts.Any())
        {
            _logger.LogWarning(
                $"The Discord ID ({discordId}) was found on other accounts during linking. These accounts will be unlinked: {string.Join(",", otherAccounts.Select(x => x.Id))}"
            );
            await _accountContext.UpdateMany(x => x.DiscordId == discordId, Builders<DomainAccount>.Update.Unset(x => x.DiscordId));
        }

        await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.DiscordId, discordId));
        var domainAccount = _accountContext.GetSingle(id);
        _eventBus.Send(domainAccount);
        _logger.LogAudit($"Discord ID ({discordId}) linked to account {domainAccount.Id}");
    }
}
