using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class DisplayNameController(IDisplayNameService displayNameService) : ControllerBase
{
    [HttpGet("{id}")]
    public string GetName([FromRoute] string id)
    {
        return displayNameService.GetDisplayName(id);
    }
}
