using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionPatchingResult {
        public int PlayerCount;
        public List<MissionPatchingReport> Reports = new();
        public bool Success;
    }
}
