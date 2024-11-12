using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public enum UnitBranch
{
    Combat,
    Auxiliary
}

public class DomainUnit : MongoObject
{
    public UnitBranch Branch { get; set; } = UnitBranch.Combat;
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

public class UnitDto : DomainUnit
{
    public string Code { get; set; }
    public string ParentName { get; set; }
    public IEnumerable<UnitMemberDto> UnitMembers { get; set; }
}

public class UnitMemberDto
{
    public string Name { get; set; }
    public string Role { get; set; }
    public string UnitRole { get; set; }
}

public class UnitTreeDto
{
    public IEnumerable<UnitTreeNodeDto> AuxiliaryNodes { get; set; }
    public IEnumerable<UnitTreeNodeDto> CombatNodes { get; set; }
}

public class UnitTreeNodeDto
{
    public List<UnitTreeNodeDto> Children { get; set; }
    public string Id { get; set; }
    public List<string> MemberIds { get; set; }
    public string MemberRole { get; set; }
    public string Name { get; set; }
    public int Order { get; set; }
    public UnitTreeNodeDto ParentUnit { get; set; }
    public bool PreferShortname { get; set; }
    public string Shortname { get; set; }
}

public class UnitChartNodeDto
{
    public IEnumerable<UnitChartNodeDto> Children { get; set; }
    public string Id { get; set; }
    public IEnumerable<UnitMemberDto> Members { get; set; }
    public string Name { get; set; }
    public bool PreferShortname { get; set; }
}

public class UnitUpdateParentDto
{
    public int Index { get; set; }
    public string ParentId { get; set; }
}

public class UnitUpdateOrderDto
{
    public int Index { get; set; }
}
