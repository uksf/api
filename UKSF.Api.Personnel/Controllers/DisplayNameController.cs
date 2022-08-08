using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers;

[Route("[controller]")]
public class DisplayNameController : ControllerBase
{
    private readonly IDisplayNameService _displayNameService;

    public DisplayNameController(IDisplayNameService displayNameService)
    {
        _displayNameService = displayNameService;
    }

    [HttpGet("{id}")]
    public string GetName(string id)
    {
        return _displayNameService.GetDisplayName(id);
    }
}
