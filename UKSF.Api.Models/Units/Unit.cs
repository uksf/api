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
        public int order;
        [BsonRepresentation(BsonType.ObjectId)] public string parent;
        [BsonRepresentation(BsonType.ObjectId)] public Dictionary<string, string> roles = new Dictionary<string, string>();
        public string shortname;
        public string teamspeakGroup;
        public bool preferShortname;

        public override string ToString() => $"{name}, {shortname}, {callsign}, {branch}, {teamspeakGroup}, {discordRoleId}";
    }

    public enum UnitBranch {
        COMBAT,
        AUXILIARY
    }

    public class ResponseUnit : Unit {
        public string code;
        public string parentName;
        public IEnumerable<ResponseUnitMember> unitMembers;
    }

    public class ResponseUnitMember {
        public string name;
        public string role;
        public string unitRole;
    }

    public class ResponseUnitTree {
        public IEnumerable<ResponseUnitTree> children;
        public string id;
        public string name;
    }

    public class ResponseUnitTreeDataSet {
        public IEnumerable<ResponseUnitTree> auxiliaryNodes;
        public IEnumerable<ResponseUnitTree> combatNodes;
    }

    public class ResponseUnitChartNode {
        public IEnumerable<ResponseUnitChartNode> children;
        public string id;
        public IEnumerable<ResponseUnitMember> members;
        public string name;
        public bool preferShortname;
    }

    public class RequestUnitUpdateParent {
        public int index;
        public string parentId;
    }

    public class RequestUnitUpdateOrder {
        public int index;
    }
}
