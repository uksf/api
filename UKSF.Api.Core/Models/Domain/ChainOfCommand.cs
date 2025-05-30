using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public class ChainOfCommand
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string First { get; set; } // 1iC

    [BsonRepresentation(BsonType.ObjectId)]
    public string Second { get; set; } // 2iC

    [BsonRepresentation(BsonType.ObjectId)]
    public string Third { get; set; } // 3iC

    [BsonRepresentation(BsonType.ObjectId)]
    public string Nco { get; set; } // NCOiC

    public bool HasMember(string memberId)
    {
        return First == memberId || Second == memberId || Third == memberId || Nco == memberId;
    }

    public void RemoveMember(string memberId)
    {
        if (First == memberId)
        {
            First = null;
        }

        if (Second == memberId)
        {
            Second = null;
        }

        if (Third == memberId)
        {
            Third = null;
        }

        if (Nco == memberId)
        {
            Nco = null;
        }
    }

    public string GetMemberAtPosition(string position)
    {
        return position switch
        {
            "1iC"   => First,
            "2iC"   => Second,
            "3iC"   => Third,
            "NCOiC" => Nco,
            _       => null
        };
    }

    public void SetMemberAtPosition(string position, string? memberId)
    {
        switch (position)
        {
            case "1iC":   First = memberId; break;
            case "2iC":   Second = memberId; break;
            case "3iC":   Third = memberId; break;
            case "NCOiC": Nco = memberId; break;
        }
    }

    public bool HasPosition(string position)
    {
        return !string.IsNullOrEmpty(GetMemberAtPosition(position));
    }

    public static int GetPositionOrder(string position)
    {
        return position switch
        {
            "1iC"   => 0,
            "2iC"   => 1,
            "3iC"   => 2,
            "NCOiC" => 3,
            _       => int.MaxValue
        };
    }

    public IEnumerable<(string Position, string MemberId)> GetAssignedPositions()
    {
        var positions = new List<(string, string)>();
        if (!string.IsNullOrEmpty(First))
        {
            positions.Add(("1iC", First));
        }

        if (!string.IsNullOrEmpty(Second))
        {
            positions.Add(("2iC", Second));
        }

        if (!string.IsNullOrEmpty(Third))
        {
            positions.Add(("3iC", Third));
        }

        if (!string.IsNullOrEmpty(Nco))
        {
            positions.Add(("NCOiC", Nco));
        }

        return positions;
    }
}
