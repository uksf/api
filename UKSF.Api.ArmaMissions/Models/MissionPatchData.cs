using System.Collections.Generic;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionPatchData {
        public static MissionPatchData instance;
        public List<MissionUnit> orderedUnits;
        public List<MissionPlayer> players;
        public List<Rank> ranks;
        public List<MissionUnit> units;
        public IEnumerable<string> medicIds;
        public IEnumerable<string> engineerIds;
    }
}
