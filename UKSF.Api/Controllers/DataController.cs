using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Admin)]
public class DataController : ControllerBase
{
    private readonly IDataCacheService _dataCacheService;

    public DataController(IDataCacheService dataCacheService)
    {
        _dataCacheService = dataCacheService;
    }

    [HttpGet("invalidate")]
    [Authorize]
    public void Invalidate()
    {
        _dataCacheService.RefreshCachedData();
    }
}
