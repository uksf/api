using System.Collections.Generic;

namespace UKSFWebsite.Api.Models.Mission {
    public class MissionPatchingResult {
        public int playerCount;
        public bool success;
        public List<MissionPatchingReport> reports = new List<MissionPatchingReport>();
    }
}
