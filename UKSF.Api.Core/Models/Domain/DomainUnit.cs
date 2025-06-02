using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public enum UnitBranch
{
    Combat,
    Auxiliary,
    Secondary
}

public class DomainUnit : MongoObject
{
    public UnitBranch Branch { get; set; } = UnitBranch.Combat;
    public string Callsign { get; set; }
    public ChainOfCommand ChainOfCommand { get; set; } = new();

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
    public string ChainOfCommandPosition { get; set; }
}

public class UnitTreeDto
{
    public IEnumerable<UnitTreeNodeDto> AuxiliaryNodes { get; set; }
    public IEnumerable<UnitTreeNodeDto> CombatNodes { get; set; }
    public IEnumerable<UnitTreeNodeDto> SecondaryNodes { get; set; }
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

public class UnitUpdateParentRequest
{
    public int Index { get; set; }
    public string ParentId { get; set; }
}

public class UnitUpdateOrderRequest
{
    public int Index { get; set; }
}
