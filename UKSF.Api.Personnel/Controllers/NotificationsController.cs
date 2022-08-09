using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationsService _notificationsService;
        private readonly IHttpContextService _httpContextService;

        public NotificationsController(INotificationsService notificationsService, IHttpContextService httpContextService)
        {
            _notificationsService = notificationsService;
            _httpContextService = httpContextService;
        }

        [HttpGet]
        [Authorize]
        public IOrderedEnumerable<Notification> Get()
        {
            return _notificationsService.GetNotificationsForContext().OrderByDescending(x => x.Timestamp);
        }

        [HttpPost("read")]
        [Authorize]
        public async Task MarkAsRead([FromBody] JObject jObject)
        {
            var ids = JArray.Parse(jObject["notifications"].ToString()).Select(notification => notification["id"].ToString()).ToList();
            await _notificationsService.MarkNotificationsAsRead(ids);
        }

        [HttpPost("clear")]
        [Authorize]
        public async Task Clear([FromBody] JObject jObject)
        {
            var clear = JArray.Parse(jObject["clear"].ToString());
            var ids = clear.Select(notification => notification["id"].ToString()).ToList();
            await _notificationsService.Delete(ids);
        }

        [HttpPost("test")]
        [Authorize]
        [Permissions(Permissions.Admin)]
        public void TestNotification()
        {
            _notificationsService.Add(
                new()
                {
                    Owner = _httpContextService.GetUserId(),
                    Icon = NotificationIcons.Comment,
                    Message = "This comment is a test:\n\"Many things were said that day but none greater than the declaration of autodefenstration\"",
                    Link = $"/recruitment/{_httpContextService.GetUserId()}"
                }
            );
        }
    }
}
