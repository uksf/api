using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UKSF.Api.Controllers;

[Route("debug")]
public class DebugController : ControllerBase
{
    [HttpGet("500")]
    [Authorize]
    public void Throw500()
    {
        throw new Exception("This is a random error");
    }
}
