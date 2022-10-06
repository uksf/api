using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Shared.Models;

// TODO: Migrate object names to names with Id
public class DomainUnit : MongoObject
{
    public UnitBranch Branch = UnitBranch.COMBAT;
    public string Callsign { get; set; }

    [BsonIgnore]
    public List<DomainUnit> Children { get; set; }

    public string DiscordRoleId { get; set; }
    public string Icon { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Members { get; set; } = new();

    public string Name { get; set; }
    public int Order { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Parent { get; set; }

    public bool PreferShortname { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public Dictionary<string, string> Roles { get; set; } = new();

    public string Shortname { get; set; }
    public string TeamspeakGroup { get; set; }

    public override string ToString()
    {
        return $"{Name}, {Shortname}, {Callsign}, {Branch}, {TeamspeakGroup}, {DiscordRoleId}";
    }
}

public class Unit
{
    public List<Unit> Children { get; set; }
    public string Id { get; set; }
    public List<string> MemberIds { get; set; }
    public string MemberRole { get; set; }
    public string Name { get; set; }
    public int Order { get; set; }
    public Unit ParentUnit { get; set; }
    public bool PreferShortname { get; set; }
    public string Shortname { get; set; }
}

public enum UnitBranch
{
    COMBAT,
    AUXILIARY
}

// TODO: Cleaner way of doing this? Inside controllers?
public class ResponseUnit : DomainUnit
{
    public string Code { get; set; }
    public string ParentName { get; set; }
    public IEnumerable<ResponseUnitMember> UnitMembers { get; set; }
}

public class ResponseUnitMember
{
    public string Name { get; set; }
    public string Role { get; set; }
    public string UnitRole { get; set; }
}

public class UnitTreeDataSet
{
    public IEnumerable<Unit> AuxiliaryNodes { get; set; }
    public IEnumerable<Unit> CombatNodes { get; set; }
}

public class ResponseUnitChartNode
{
    public IEnumerable<ResponseUnitChartNode> Children { get; set; }
    public string Id { get; set; }
    public IEnumerable<ResponseUnitMember> Members { get; set; }
    public string Name { get; set; }
    public bool PreferShortname { get; set; }
}

public class RequestUnitUpdateParent
{
    public int Index { get; set; }
    public string ParentId { get; set; }
}

public class RequestUnitUpdateOrder
{
    public int Index { get; set; }
}
