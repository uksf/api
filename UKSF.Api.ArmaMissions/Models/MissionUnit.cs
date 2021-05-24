using System.Collections.Generic;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.ArmaMissions.Models
{
    public class MissionUnit
    {
        public string Callsign;
        public List<MissionPlayer> Members = new();
        public Dictionary<string, MissionPlayer> Roles = new();
        public Unit SourceUnit;
    }
}
