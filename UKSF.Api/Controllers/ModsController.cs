using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Confirmed, Permissions.Member)]
public class ModsController : ControllerBase
{
    // TODO: Return size of modpack folder
    [HttpGet("size")]
    public string Index()
    {
        return "37580963840";
    }
}
