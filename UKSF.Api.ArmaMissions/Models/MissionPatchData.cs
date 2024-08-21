using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.ArmaMissions.Models;

public class MissionPatchData
{
    public static MissionPatchData Instance { get; set; }
    public List<MissionUnit> OrderedUnits { get; set; }
    public List<MissionPlayer> Players { get; set; }
    public List<DomainRank> Ranks { get; set; }
    public List<MissionUnit> Units { get; set; }
}
