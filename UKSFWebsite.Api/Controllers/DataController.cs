using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Services.Personnel;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
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
