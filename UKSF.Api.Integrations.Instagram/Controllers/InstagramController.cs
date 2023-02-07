using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Integrations.Instagram.Models;
using UKSF.Api.Integrations.Instagram.ScheduledActions;
using UKSF.Api.Integrations.Instagram.Services;

namespace UKSF.Api.Integrations.Instagram.Controllers;

[Route("[controller]")]
public class InstagramController : ControllerBase
{
    private readonly IActionInstagramToken _actionInstagramToken;
    private readonly IInstagramService _instagramService;

    public InstagramController(IInstagramService instagramService, IActionInstagramToken actionInstagramToken)
    {
        _instagramService = instagramService;
        _actionInstagramToken = actionInstagramToken;
    }

    [HttpGet]
    public IEnumerable<InstagramImage> GetImages()
    {
        return _instagramService.GetImages();
    }

    [HttpGet("refreshToken")]
    [Permissions(Permissions.Admin)]
    public async Task RefreshToken()
    {
        await _actionInstagramToken.Reset();
    }
}
