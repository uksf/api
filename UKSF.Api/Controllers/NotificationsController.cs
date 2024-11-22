using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class NotificationsController(INotificationsService notificationsService, IHttpContextService httpContextService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IOrderedEnumerable<DomainNotification> Get()
    {
        return notificationsService.GetNotificationsForContext().OrderByDescending(x => x.Timestamp);
    }

    [HttpPost("read")]
    [Authorize]
    public async Task MarkAsRead([FromBody] NotificationsRequest notificationsRequest)
    {
        var ids = notificationsRequest.Notifications.Select(x => x.Id).ToList();
        await notificationsService.MarkNotificationsAsRead(ids);
    }

    [HttpPost("clear")]
    [Authorize]
    public async Task Clear([FromBody] NotificationsRequest notificationsRequest)
    {
        var ids = notificationsRequest.Notifications.Select(x => x.Id).ToList();
        await notificationsService.Delete(ids);
    }

    [HttpPost("test")]
    [Authorize]
    [Permissions(Permissions.Admin)]
    public void TestNotification()
    {
        notificationsService.Add(
            new DomainNotification
            {
                Owner = httpContextService.GetUserId(),
                Icon = NotificationIcons.Comment,
                Message = "This comment is a test:\n\"Many things were said that day but none greater than the declaration of autodefenstration\"",
                Link = $"/recruitment/{httpContextService.GetUserId()}"
            }
        );
    }
}
