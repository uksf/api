using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IHttpContextService _httpContextService;
    private readonly INotificationsService _notificationsService;

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
    public async Task MarkAsRead([FromBody] NotificationsRequest notificationsRequest)
    {
        var ids = notificationsRequest.Notifications.Select(x => x.Id).ToList();
        await _notificationsService.MarkNotificationsAsRead(ids);
    }

    [HttpPost("clear")]
    [Authorize]
    public async Task Clear([FromBody] NotificationsRequest notificationsRequest)
    {
        var ids = notificationsRequest.Notifications.Select(x => x.Id).ToList();
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
