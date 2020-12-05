using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public record Unit : MongoObject {
        public UnitBranch Branch { get; set; } = UnitBranch.COMBAT;
        public string Callsign { get; set; }
        public string DiscordRoleId { get; set; }
        public string Icon { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public List<string> Members { get; set; } = new();
        public string Name { get; set; }
        public int Order { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public string Parent { get; set; }
        public bool PreferShortname { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public Dictionary<string, string> Roles { get; set; } = new();
        public string Shortname { get; set; }
        public string TeamspeakGroup { get; set; }

        public override string ToString() => $"{Name}, {Shortname}, {Callsign}, {Branch}, {TeamspeakGroup}, {DiscordRoleId}";
    }

    public enum UnitBranch {
        COMBAT,
        AUXILIARY
    }

    // TODO: Cleaner way of doing this? Inside controllers?
    public record ResponseUnit : Unit {
        public string Code { get; set; }
        public string ParentName { get; set; }
        public IEnumerable<ResponseUnitMember> UnitMembers { get; set; }
    }

    public class ResponseUnitMember {
        public string Name { get; set; }
        public string Role { get; set; }
        public string UnitRole { get; set; }
    }

    public class ResponseUnitTree {
        public IEnumerable<ResponseUnitTree> Children { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class ResponseUnitTreeDataSet {
        public IEnumerable<ResponseUnitTree> AuxiliaryNodes { get; set; }
        public IEnumerable<ResponseUnitTree> CombatNodes { get; set; }
    }

    public class ResponseUnitChartNode {
        public IEnumerable<ResponseUnitChartNode> Children { get; set; }
        public string Id { get; set; }
        public IEnumerable<ResponseUnitMember> Members { get; set; }
        public string Name { get; set; }
        public bool PreferShortname { get; set; }
    }

    public class RequestUnitUpdateParent {
        public int Index { get; set; }
        public string ParentId { get; set; }
    }

    public class RequestUnitUpdateOrder {
        public int Index { get; set; }
    }
}
