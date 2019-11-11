using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Interfaces.Message;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class NotificationsController : Controller {
        private readonly INotificationsService notificationsService;

        public NotificationsController(INotificationsService notificationsService) => this.notificationsService = notificationsService;

        [HttpGet, Authorize]
        public IActionResult Get() {
            return Ok(notificationsService.GetNotificationsForContext().OrderByDescending(x => x.timestamp));
        }

        [HttpPost("read"), Authorize]
        public async Task<IActionResult> MarkAsRead([FromBody] JObject jObject) {
            IEnumerable<string> ids = JArray.Parse(jObject["notifications"].ToString()).Select(notification => notification["id"].ToString());
            await notificationsService.MarkNotificationsAsRead(ids);
            return Ok();
        }

        [HttpPost("clear"), Authorize]
        public async Task<IActionResult> Clear([FromBody] JObject jObject) {
            JArray clear = JArray.Parse(jObject["clear"].ToString());
            IEnumerable<string> ids = clear.Select(notification => notification["id"].ToString());
            await notificationsService.Delete(ids);
            return Ok();
        }
    }
}
