using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Commands;

namespace UKSF.Api.Controllers;

[Route("accounts")]
[Permissions(Permissions.Command)]
public class AccountsTrainingController(IUpdateAccountTrainingCommandHandler accountTrainingCommandHandler) : ControllerBase
{
    [HttpPut("{accountId}/training")]
    public Task UpdateAccountTraining([FromRoute] string accountId, [FromBody] List<string> trainingIds)
    {
        return accountTrainingCommandHandler.ExecuteAsync(new UpdateAccountTrainingCommand(accountId, trainingIds));
    }

    [HttpPatch("training")]
    public Task BulkUpdateAccountTraining([FromBody] BulkUpdateAccountTrainingCommand command)
    {
        return accountTrainingCommandHandler.ExecuteAsync(command);
    }
}
