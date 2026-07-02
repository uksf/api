using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum CampaignStatus
{
    Current,
    Past,
    Upcoming
}

public class DomainCampaign : MongoObject
{
    public string Name { get; set; }
    public string Summary { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Upcoming;
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
}
