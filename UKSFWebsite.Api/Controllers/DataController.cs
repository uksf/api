using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.ADMIN)]
    public class DataController : Controller {
        private readonly CacheService cacheService;

        public DataController(CacheService cacheService) => this.cacheService = cacheService;
        
        [HttpGet("invalidate"), Authorize]
        public IActionResult Invalidate() {
            cacheService.InvalidateCaches();
            return Ok();
        }
    }
}
