using System.Collections.Generic;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionUnit {
        public string callsign;
        public List<MissionPlayer> members = new List<MissionPlayer>();
        public Dictionary<string, MissionPlayer> roles = new Dictionary<string, MissionPlayer>();
        public Unit sourceUnit;
    }
}
