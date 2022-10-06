using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class DisplayNameController : ControllerBase
{
    private readonly IDisplayNameService _displayNameService;

    public DisplayNameController(IDisplayNameService displayNameService)
    {
        _displayNameService = displayNameService;
    }

    [HttpGet("{id}")]
    public string GetName([FromRoute] string id)
    {
        return _displayNameService.GetDisplayName(id);
    }
}
