using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Admin.Services;

namespace UKSF.Api.Admin.Controllers {
    [Route("[controller]")]
    public class DebugController : Controller {
        private readonly IHostEnvironment currentEnvironment;
        private readonly DataCacheService dataCacheService;

        public DebugController(IHostEnvironment currentEnvironment, DataCacheService dataCacheService) {
            this.currentEnvironment = currentEnvironment;
            this.dataCacheService = dataCacheService;
        }

        // TODO: Should be in notifcation controller
        // [HttpGet("notifications-test"), Authorize]
        // public IActionResult NotificationsTest() {
        //     if (!currentEnvironment.IsDevelopment()) return Ok();
        //
        //     notificationsService.Add(
        //         new Notification {
        //             owner = httpContextService.GetContextId(), message = $"This is a test notification. The time is {DateTime.Now:HH:mm:ss}", timestamp = DateTime.Now, icon = NotificationIcons.REQUEST
        //         }
        //     );
        //     return Ok();
        // }

        [HttpGet("invalidate-data")]
        public IActionResult InvalidateData() {
            if (!currentEnvironment.IsDevelopment()) return Ok();

            dataCacheService.RefreshCachedData();
            return Ok();
        }
    }
}
