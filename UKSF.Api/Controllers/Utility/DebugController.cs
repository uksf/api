using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Controllers.Utility {
    [Route("[controller]")]
    public class DebugController : Controller {
        private readonly INotificationsService notificationsService;
        private readonly ISessionService sessionService;

        public DebugController(INotificationsService notificationsService, ISessionService sessionService) {
            this.notificationsService = notificationsService;
            this.sessionService = sessionService;
        }

        [HttpGet("notifications-test"), Authorize]
        public IActionResult NotificationsTest() {
            notificationsService.Add(
                new Notification {
                    owner = sessionService.GetContextId(), message = $"This is a test notification. The time is {DateTime.Now:HH:mm:ss}", timestamp = DateTime.Now, icon = NotificationIcons.REQUEST
                }
            );
            return Ok();
        }
    }
}
