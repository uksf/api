using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Controllers;

[Route("accounts/{id}/qualifications")]
[Authorize]
public class QualificationsController(IQualificationsUpdateCommand qualificationsUpdateCommand) : ControllerBase
{
    [HttpPut]
    [Permissions(Permissions.Command)]
    public async Task UpdateQualifications([FromRoute] string id, [FromBody] Qualifications qualifications)
    {
        await qualificationsUpdateCommand.ExecuteAsync(id, qualifications);
    }
}
