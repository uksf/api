using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Commands;

namespace UKSF.Api.Controllers;

[Route("accounts")]
public class AccountsTrainingController(IUpdateAccountTrainingCommandHandler accountTrainingCommandHandler) : ControllerBase
{
    [HttpPut("{accountId}/training")]
    [Authorize]
    public Task UpdateAccountTraining([FromRoute] string accountId, [FromBody] List<string> trainingIds)
    {
        return accountTrainingCommandHandler.ExecuteAsync(new UpdateAccountTrainingCommand(accountId, trainingIds));
    }

    [HttpPatch("training")]
    [Permissions(Permissions.Command)]
    public Task BulkUpdateAccountTraining([FromBody] BulkUpdateAccountTrainingCommand command)
    {
        return accountTrainingCommandHandler.ExecuteAsync(command);
    }
}
