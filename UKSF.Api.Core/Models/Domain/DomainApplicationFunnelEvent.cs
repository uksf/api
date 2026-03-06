namespace UKSF.Api.Core.Models.Domain;

public class DomainApplicationFunnelEvent : MongoObject
{
    public string VisitorId { get; set; }
    public string Event { get; set; }
    public double? Value { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserAgent { get; set; }
    public string AccountId { get; set; }
}
