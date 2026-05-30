using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Controllers;

[Route("scheduled-actions")]
[Permissions(Permissions.Admin)]
public class ScheduledActionsController(IScheduledActionFactory scheduledActionFactory) : ControllerBase
{
    [HttpPost("{name}/run")]
    public async Task<IActionResult> Run(string name)
    {
        try
        {
            var action = scheduledActionFactory.GetScheduledAction(name);
            await action.Run();
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
