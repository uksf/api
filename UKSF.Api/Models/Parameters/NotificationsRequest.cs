using UKSF.Api.Shared.Models;

namespace UKSF.Api.Models.Parameters;

public class NotificationsRequest
{
    public List<Notification> Notifications { get; set; }
}
