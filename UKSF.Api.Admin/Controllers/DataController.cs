using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Admin.Services;
using UKSF.Api.Shared;

namespace UKSF.Api.Admin.Controllers {
    [Route("[controller]"), Permissions(Permissions.ADMIN)]
    public class DataController : Controller {
        private readonly IDataCacheService _dataCacheService;

        public DataController(IDataCacheService dataCacheService) => _dataCacheService = dataCacheService;

        [HttpGet("invalidate"), Authorize]
        public IActionResult Invalidate() {
            _dataCacheService.RefreshCachedData();
            return Ok();
        }
    }
}
