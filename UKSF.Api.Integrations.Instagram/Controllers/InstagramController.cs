using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Integrations.Instagram.Models;
using UKSF.Api.Integrations.Instagram.Services;

namespace UKSF.Api.Integrations.Instagram.Controllers;

[Route("[controller]")]
public class InstagramController(IInstagramService instagramService) : ControllerBase
{
    [HttpGet]
    public Task<List<InstagramImage>> GetImages()
    {
        return instagramService.GetImagesFromLocalCache();
    }

    [HttpGet("cache")]
    [Permissions(Permissions.Admin)]
    public async Task Cache()
    {
        await instagramService.CacheInstagramImages();
    }
}
