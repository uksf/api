using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionPatchingResult {
        public int playerCount;
        public List<MissionPatchingReport> reports = new List<MissionPatchingReport>();
        public bool success;
    }
}
