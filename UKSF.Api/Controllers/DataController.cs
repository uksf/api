using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Admin)]
public class DataController(IDataCacheService dataCacheService) : ControllerBase
{
    [HttpGet("invalidate")]
    [Authorize]
    public void Invalidate()
    {
        dataCacheService.RefreshCachedData();
    }
}
