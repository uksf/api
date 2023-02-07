using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class SteamCodeController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IConfirmationCodeService _confirmationCodeService;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;

    public SteamCodeController(
        IAccountContext accountContext,
        IConfirmationCodeService confirmationCodeService,
        IHttpContextService httpContextService,
        IUksfLogger logger
    )
    {
        _accountContext = accountContext;
        _confirmationCodeService = confirmationCodeService;
        _httpContextService = httpContextService;
        _logger = logger;
    }

    [HttpPost("{steamId}")]
    [Authorize]
    public async Task SteamConnect([FromRoute] string steamId, [FromBody] SteamCodeRequest steamCode)
    {
        var value = await _confirmationCodeService.GetConfirmationCodeValue(steamCode.Code);
        if (string.IsNullOrEmpty(value) || value != steamId)
        {
            throw new InvalidConfirmationCodeException();
        }

        var id = _httpContextService.GetUserId();
        await _accountContext.Update(id, x => x.Steamname, steamId);
        var domainAccount = _accountContext.GetSingle(id);
        _logger.LogAudit($"SteamID updated for {domainAccount.Id} to {steamId}");
    }
}
