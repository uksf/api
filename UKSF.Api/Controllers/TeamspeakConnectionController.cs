using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Commands;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Controllers;

[Route("accounts/{accountId}")]
public class TeamspeakConnectionController(IConnectTeamspeakIdToAccountCommand connectTeamspeakIdToAccountCommand, IAccountMapper accountMapper)
    : ControllerBase
{
    [HttpPost("teamspeak/{teamspeakId}")]
    [Authorize]
    public async Task<Account> ConnectTeamspeakCode([FromRoute] string accountId, [FromRoute] string teamspeakId, [FromBody] TeamspeakCode teamspeakCode)
    {
        var updatedAccount = await connectTeamspeakIdToAccountCommand.ExecuteAsync(accountId, teamspeakId, teamspeakCode.Code);
        return accountMapper.MapToAccount(updatedAccount);
    }
}
