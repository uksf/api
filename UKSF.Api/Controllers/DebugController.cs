using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UKSF.Api.Controllers;

[Route("debug")]
public class DebugController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet("500")]
    [Authorize]
    public void Throw500()
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Debug endpoints are only available in Development");
        }

        throw new Exception("This is a random error");
    }
}
