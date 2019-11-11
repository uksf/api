using System.Collections.Generic;
using UKSFWebsite.Api.Models.Units;

namespace UKSFWebsite.Api.Models.Mission {
    public class MissionUnit {
        public string callsign;
        public int depth;
        public List<MissionPlayer> members = new List<MissionPlayer>();
        public Dictionary<string, MissionPlayer> roles = new Dictionary<string, MissionPlayer>();
        public Unit sourceUnit;
    }
}
