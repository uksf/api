using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Commands;
using UKSF.Api.Models.Parameters;
using UKSF.Api.Shared.Mappers;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Controllers;

[Route("accounts/{accountId}")]
public class TeamspeakConnectionController : ControllerBase
{
    private readonly IAccountMapper _accountMapper;
    private readonly IConnectTeamspeakIdToAccountCommand _connectTeamspeakIdToAccountCommand;

    public TeamspeakConnectionController(IConnectTeamspeakIdToAccountCommand connectTeamspeakIdToAccountCommand, IAccountMapper accountMapper)
    {
        _connectTeamspeakIdToAccountCommand = connectTeamspeakIdToAccountCommand;
        _accountMapper = accountMapper;
    }

    [HttpPost("teamspeak/{teamspeakId}")]
    [Authorize]
    public async Task<Account> ConnectTeamspeakCode([FromRoute] string accountId, [FromRoute] string teamspeakId, [FromBody] TeamspeakCode teamspeakCode)
    {
        var updatedAccount = await _connectTeamspeakIdToAccountCommand.ExecuteAsync(new(accountId, teamspeakId, teamspeakCode.Code));
        return _accountMapper.MapToAccount(updatedAccount);
    }
}
