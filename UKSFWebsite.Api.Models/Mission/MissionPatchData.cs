using System.Collections.Generic;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Models.Mission {
    public class MissionPatchData {
        public static MissionPatchData instance;
        public List<MissionUnit> orderedUnits;
        public List<MissionPlayer> players;
        public List<Rank> ranks;
        public List<MissionUnit> units;
    }
}
