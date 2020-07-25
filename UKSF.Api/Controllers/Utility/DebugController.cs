using System;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.Controllers.Utility {
    [Route("[controller]")]
    public class DebugController : Controller {
        private readonly IHostEnvironment currentEnvironment;
        private readonly DataCacheService dataCacheService;
        private readonly INotificationsService notificationsService;
        private readonly ISessionService sessionService;

        public DebugController(INotificationsService notificationsService, ISessionService sessionService, IHostEnvironment currentEnvironment, DataCacheService dataCacheService) {
            this.notificationsService = notificationsService;
            this.sessionService = sessionService;
            this.currentEnvironment = currentEnvironment;
            this.dataCacheService = dataCacheService;
        }

        [HttpGet("notifications-test"), Authorize]
        public IActionResult NotificationsTest() {
            if (!currentEnvironment.IsDevelopment()) return Ok();

            notificationsService.Add(
                new Notification {
                    owner = sessionService.GetContextId(), message = $"This is a test notification. The time is {DateTime.Now:HH:mm:ss}", timestamp = DateTime.Now, icon = NotificationIcons.REQUEST
                }
            );
            return Ok();
        }

        [HttpGet("invalidate-data")]
        public IActionResult InvalidateData() {
            if (!currentEnvironment.IsDevelopment()) return Ok();

            dataCacheService.InvalidateCachedData();
            return Ok();
        }
    }
}
