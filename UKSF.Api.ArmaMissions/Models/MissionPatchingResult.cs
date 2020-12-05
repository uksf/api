using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionPatchingResult {
        public int PlayerCount { get; set; }
        public List<MissionPatchingReport> Reports { get; set; } = new();
        public bool Success { get; set; }
    }
}
