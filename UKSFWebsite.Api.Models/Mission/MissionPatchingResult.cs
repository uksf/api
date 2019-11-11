using System.Collections.Generic;

namespace UKSFWebsite.Api.Models.Mission {
    public class MissionPatchingResult {
        public int playerCount;
        public List<MissionPatchingReport> reports = new List<MissionPatchingReport>();
        public bool success;
    }
}
