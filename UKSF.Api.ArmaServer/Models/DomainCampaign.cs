using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum CampaignStatus
{
    Active,
    Archived
}

public class DomainCampaign : MongoObject
{
    public string Name { get; set; }
    public string Brief { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Active;
    public string Theatre { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
}
