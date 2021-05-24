using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Personnel.Commands;
using UKSF.Api.Personnel.Mappers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Models.Parameters;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("accounts/{accountId}")]
    public class TeamspeakConnectionController : Controller
    {
        private readonly IAccountMapper _accountMapper;
        private readonly IConnectTeamspeakIdToAccountCommand _connectTeamspeakIdToAccountCommand;

        public TeamspeakConnectionController(IConnectTeamspeakIdToAccountCommand connectTeamspeakIdToAccountCommand, IAccountMapper accountMapper)
        {
            _connectTeamspeakIdToAccountCommand = connectTeamspeakIdToAccountCommand;
            _accountMapper = accountMapper;
        }

        [HttpPost("teamspeak/{teamspeakId}"), Authorize]
        public async Task<Account> ConnectTeamspeakCode([FromRoute] string accountId, [FromRoute] string teamspeakId, [FromBody] TeamspeakCode teamspeakCode)
        {
            DomainAccount updatedAccount = await _connectTeamspeakIdToAccountCommand.ExecuteAsync(new(accountId, teamspeakId, teamspeakCode.Code));
            return _accountMapper.MapToAccount(updatedAccount);
        }
    }
}
