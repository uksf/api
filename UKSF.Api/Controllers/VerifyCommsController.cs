using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Controllers;

[Route("accounts/verify")]
public class VerifyCommsController(
    IAccountContext accountContext,
    IHttpContextService httpContextService,
    IAccountMapper accountMapper,
    VerifyMode verifyMode
) : ControllerBase
{
    public const int SeededTeamspeakIdentity = -1;
    public const string SeededPrefix = "verify-";

    [HttpPost("comms")]
    [Authorize]
    public async Task<Account> SeedComms()
    {
        if (!verifyMode.Enabled)
        {
            throw new NotFoundException("Not found");
        }

        var id = httpContextService.GetUserId();
        await accountContext.Update(
            id,
            Builders<DomainAccount>.Update.Set(x => x.TeamspeakIdentities, [SeededTeamspeakIdentity])
                                   .Set(x => x.Steamname, $"{SeededPrefix}{id}")
                                   .Set(x => x.DiscordId, $"{SeededPrefix}{id}")
        );

        return accountMapper.MapToAccount(accountContext.GetSingle(id));
    }
}
