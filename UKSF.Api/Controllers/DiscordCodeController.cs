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
public class DiscordCodeController(
    IAccountContext accountContext,
    IConfirmationCodeService confirmationCodeService,
    IHttpContextService httpContextService,
    IEventBus eventBus,
    IUksfLogger logger
) : ControllerBase
{
    [HttpPost("{discordId}")]
    [Authorize]
    public async Task DiscordConnect([FromRoute] string discordId, [FromBody] DiscordCodeRequest discordCode)
    {
        var value = await confirmationCodeService.GetConfirmationCodeValue(discordCode.Code);
        if (string.IsNullOrEmpty(value) || value != discordId)
        {
            throw new DiscordConnectFailedException();
        }

        var id = httpContextService.GetUserId();
        var otherAccounts = accountContext.Get(x => x.Id != id && x.DiscordId == discordId).ToList();
        if (otherAccounts.Any())
        {
            logger.LogWarning(
                $"The Discord ID ({discordId}) was found on other accounts during linking. These accounts will be unlinked: {string.Join(",", otherAccounts.Select(x => x.Id))}"
            );
            await accountContext.UpdateMany(x => x.DiscordId == discordId, Builders<DomainAccount>.Update.Unset(x => x.DiscordId));
        }

        await accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.DiscordId, discordId));
        var domainAccount = accountContext.GetSingle(id);
        eventBus.Send(new ContextEventData<DomainAccount>(id, domainAccount));
        logger.LogAudit($"Discord ID ({discordId}) linked to account {domainAccount.Id}");
    }
}
