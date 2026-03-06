namespace UKSF.Api.Models.Request;

public class TrackFunnelEventRequest
{
    public string VisitorId { get; set; }
    public string Event { get; set; }
    public double? Value { get; set; }
}
