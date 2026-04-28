using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Services;

namespace UKSF.Api.ArmaServer.Controllers;

public record DevRunLogRequest(string Line);

[ApiController]
[Route("dev-run/internal")]
public class DevRunInternalController(IDevRunService service) : ControllerBase
{
    [HttpPost("{runId}/log")]
    public async Task<IActionResult> AppendLog(string runId, [FromBody] DevRunLogRequest body)
    {
        await service.AppendLogAsync(runId, body.Line ?? "");
        return NoContent();
    }

    [HttpPost("{runId}/result")]
    public async Task<IActionResult> AppendResult(string runId)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        await service.AppendResultAsync(runId, payload);
        return NoContent();
    }

    [HttpPost("{runId}/finish")]
    public async Task<IActionResult> Finish(string runId)
    {
        await service.FinishAsync(runId);
        return NoContent();
    }
}
