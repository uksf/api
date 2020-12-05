using System.Collections.Generic;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionUnit {
        public string Callsign { get; set; }
        public List<MissionPlayer> Members { get; set; } = new();
        public Dictionary<string, MissionPlayer> Roles { get; set; } = new();
        public Unit SourceUnit { get; set; }
    }
}
