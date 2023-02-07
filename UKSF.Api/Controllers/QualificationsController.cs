using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Controllers;

[Route("accounts/{id}/qualifications")]
[Authorize]
public class QualificationsController : ControllerBase
{
    private readonly IQualificationsUpdateCommand _qualificationsUpdateCommand;

    public QualificationsController(IQualificationsUpdateCommand qualificationsUpdateCommand)
    {
        _qualificationsUpdateCommand = qualificationsUpdateCommand;
    }

    [HttpPut]
    [Permissions(Permissions.Command)]
    public async Task UpdateQualifications([FromRoute] string id, [FromBody] Qualifications qualifications)
    {
        await _qualificationsUpdateCommand.ExecuteAsync(id, qualifications);
    }
}
