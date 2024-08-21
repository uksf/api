using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Models.Request;

public class NotificationsRequest
{
    public List<DomainNotification> Notifications { get; set; }
}
