using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core.Commands;

namespace UKSF.Api.Controllers;

[Route("accounts/{accountId}/training")]
public class AccountsTrainingController(IUpdateAccountTrainingCommandHandler accountTrainingCommandHandler) : ControllerBase
{
    [HttpPut]
    [Authorize]
    public Task UpdateAccountTraining([FromRoute] string accountId, [FromBody] List<string> trainingIds)
    {
        return accountTrainingCommandHandler.ExecuteAsync(new UpdateAccountTrainingCommand(accountId, trainingIds));
    }
}
