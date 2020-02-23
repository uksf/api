using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Units {
    public class Unit : DatabaseObject {
        public UnitBranch branch = UnitBranch.COMBAT;
        public string callsign;
        public string discordRoleId;
        public string icon;
        [BsonRepresentation(BsonType.ObjectId)] public List<string> members = new List<string>();
        public string name;
        public int order = 0;
        [BsonRepresentation(BsonType.ObjectId)] public string parent;
        [BsonRepresentation(BsonType.ObjectId)] public Dictionary<string, string> roles = new Dictionary<string, string>();
        public string shortname;
        public string teamspeakGroup;
        public UnitType type;
    }

    public enum UnitType {
        SRTEAM,
        SECTION,
        PLATOON,
        COMPANY,
        BATTALION,
        REGIMENT,
        TASKFORCE,
        CREW,
        FLIGHT,
        SQUADRON,
        WING,
        GROUP
    }

    public enum UnitBranch {
        COMBAT,
        AUXILIARY
    }
}
