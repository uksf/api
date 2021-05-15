using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models
{
    public class Unit : MongoObject
    {
        public UnitBranch Branch = UnitBranch.COMBAT;
        public string Callsign;
        public string DiscordRoleId;
        public string Icon;
        [BsonRepresentation(BsonType.ObjectId)] public List<string> Members = new();
        public string Name;
        public int Order;
        [BsonRepresentation(BsonType.ObjectId)] public string Parent;
        public bool PreferShortname;
        [BsonRepresentation(BsonType.ObjectId)] public Dictionary<string, string> Roles = new();
        public string Shortname;
        public string TeamspeakGroup;

        public override string ToString()
        {
            return $"{Name}, {Shortname}, {Callsign}, {Branch}, {TeamspeakGroup}, {DiscordRoleId}";
        }
    }

    public enum UnitBranch
    {
        COMBAT,
        AUXILIARY
    }

    // TODO: Cleaner way of doing this? Inside controllers?
    public class ResponseUnit : Unit
    {
        public string Code;
        public string ParentName;
        public IEnumerable<ResponseUnitMember> UnitMembers;
    }

    public class ResponseUnitMember
    {
        public string Name;
        public string Role;
        public string UnitRole;
    }

    public class ResponseUnitTree
    {
        public IEnumerable<ResponseUnitTree> Children;
        public string Id;
        public string Name;
    }

    public class ResponseUnitTreeDataSet
    {
        public IEnumerable<ResponseUnitTree> AuxiliaryNodes;
        public IEnumerable<ResponseUnitTree> CombatNodes;
    }

    public class ResponseUnitChartNode
    {
        public IEnumerable<ResponseUnitChartNode> Children;
        public string Id;
        public IEnumerable<ResponseUnitMember> Members;
        public string Name;
        public bool PreferShortname;
    }

    public class RequestUnitUpdateParent
    {
        public int Index;
        public string ParentId;
    }

    public class RequestUnitUpdateOrder
    {
        public int Index;
    }
}
