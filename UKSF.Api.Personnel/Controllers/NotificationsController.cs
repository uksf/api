using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Models.Parameters;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers;

[Route("[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationsService _notificationsService;

    public NotificationsController(INotificationsService notificationsService)
    {
        _notificationsService = notificationsService;
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
}
