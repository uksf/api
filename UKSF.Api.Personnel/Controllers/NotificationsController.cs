using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("[controller]")]
    public class NotificationsController : Controller
    {
        private readonly INotificationsService _notificationsService;

        public NotificationsController(INotificationsService notificationsService)
        {
            _notificationsService = notificationsService;
        }

        [HttpGet, Authorize]
        public IOrderedEnumerable<Notification> Get()
        {
            return _notificationsService.GetNotificationsForContext().OrderByDescending(x => x.Timestamp);
        }

        [HttpPost("read"), Authorize]
        public async Task MarkAsRead([FromBody] JObject jObject)
        {
            List<string> ids = JArray.Parse(jObject["notifications"].ToString()).Select(notification => notification["id"].ToString()).ToList();
            await _notificationsService.MarkNotificationsAsRead(ids);
        }

        [HttpPost("clear"), Authorize]
        public async Task Clear([FromBody] JObject jObject)
        {
            JArray clear = JArray.Parse(jObject["clear"].ToString());
            List<string> ids = clear.Select(notification => notification["id"].ToString()).ToList();
            await _notificationsService.Delete(ids);
        }
    }
}
