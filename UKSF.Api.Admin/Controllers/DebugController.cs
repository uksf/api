using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Admin.Services;

namespace UKSF.Api.Admin.Controllers {
    [Route("[controller]")]
    public class DebugController : Controller {
        private readonly IHostEnvironment _currentEnvironment;
        private readonly DataCacheService _dataCacheService;

        public DebugController(IHostEnvironment currentEnvironment, DataCacheService dataCacheService) {
            _currentEnvironment = currentEnvironment;
            _dataCacheService = dataCacheService;
        }

        [HttpGet("invalidate-data")]
        public IActionResult InvalidateData() {
            if (!_currentEnvironment.IsDevelopment()) return Ok();

            _dataCacheService.RefreshCachedData();
            return Ok();
        }
    }
}
