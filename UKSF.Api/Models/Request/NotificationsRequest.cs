using UKSF.Api.Core.Models;

namespace UKSF.Api.Models.Request;

public class NotificationsRequest
{
    public List<Notification> Notifications { get; set; }
}
