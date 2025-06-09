using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class SteamCodeController(
    IAccountContext accountContext,
    IConfirmationCodeService confirmationCodeService,
    IHttpContextService httpContextService,
    IUksfLogger logger
) : ControllerBase
{
    [HttpPost("{steamId}")]
    [Authorize]
    public async Task SteamConnect([FromRoute] string steamId, [FromBody] SteamCodeRequest steamCode)
    {
        var value = await confirmationCodeService.GetConfirmationCodeValue(steamCode.Code);
        if (string.IsNullOrEmpty(value) || value != steamId)
        {
            throw new InvalidConfirmationCodeException();
        }

        var id = httpContextService.GetUserId();
        await accountContext.Update(id, x => x.Steamname, steamId);
        var account = accountContext.GetSingle(id);
        logger.LogAudit($"SteamID updated for {account.Id} to {steamId}");
    }
}
