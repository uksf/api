using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.ADMIN)]
    public class DataController : Controller {
        private readonly DataCacheService dataCacheService;

        public DataController(DataCacheService dataCacheService) => this.dataCacheService = dataCacheService;

        [HttpGet("invalidate"), Authorize]
        public IActionResult Invalidate() {
            dataCacheService.InvalidateDataCaches();
            return Ok();
        }
    }
}