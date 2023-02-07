using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Models;

public class MissionPlayer
{
    public DomainAccount DomainAccount { get; set; }
    public string Name { get; set; }
    public string ObjectClass { get; set; }
    public DomainRank Rank { get; set; }
    public MissionUnit Unit { get; set; }
}
