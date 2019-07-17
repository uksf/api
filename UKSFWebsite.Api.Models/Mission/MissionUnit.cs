using System.Collections.Generic;

namespace UKSFWebsite.Api.Models.Mission {
    public class MissionUnit {
        public string callsign;
        public List<MissionPlayer> members = new List<MissionPlayer>();
        public Dictionary<string, MissionPlayer> roles = new Dictionary<string, MissionPlayer>();
        public Unit sourceUnit;
        public int depth;
    }
}
