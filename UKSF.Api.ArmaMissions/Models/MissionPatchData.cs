using System.Collections.Generic;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.ArmaMissions.Models
{
    public class MissionPatchData
    {
        public static MissionPatchData Instance;
        public IEnumerable<string> EngineerIds;
        public IEnumerable<string> MedicIds;
        public List<MissionUnit> OrderedUnits;
        public List<MissionPlayer> Players;
        public List<DomainRank> Ranks;
        public List<MissionUnit> Units;
    }
}
