using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base;

namespace UKSF.Api.Admin.Controllers {
    [Route("[controller]"), Permissions(Permissions.ADMIN)]
    public class DataController : Controller {
        private readonly DataCacheService dataCacheService;

        public DataController(DataCacheService dataCacheService) => this.dataCacheService = dataCacheService;

        [HttpGet("invalidate"), Authorize]
        public IActionResult Invalidate() {
            dataCacheService.InvalidateCachedData();
            return Ok();
        }
    }
}
