using System.ComponentModel.DataAnnotations;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Models.Request;

public class NotificationsRequest
{
    [Required]
    public List<DomainNotification> Notifications { get; set; }
}
