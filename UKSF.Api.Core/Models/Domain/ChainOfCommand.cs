using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public class ChainOfCommand
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string OneIC { get; set; } // 1iC (Order 0)

    [BsonRepresentation(BsonType.ObjectId)]
    public string TwoIC { get; set; } // 2iC (Order 1)

    [BsonRepresentation(BsonType.ObjectId)]
    public string ThreeIC { get; set; } // 3iC (Order 2)

    [BsonRepresentation(BsonType.ObjectId)]
    public string NCOIC { get; set; } // NCOiC (Order 3)

    public bool HasMember(string memberId) => OneIC == memberId || TwoIC == memberId || ThreeIC == memberId || NCOIC == memberId;

    public void RemoveMember(string memberId)
    {
        if (OneIC == memberId) OneIC = null;
        if (TwoIC == memberId) TwoIC = null;
        if (ThreeIC == memberId) ThreeIC = null;
        if (NCOIC == memberId) NCOIC = null;
    }

    public string? GetMemberAtPosition(string position) =>
        position switch
        {
            "1iC"   => OneIC,
            "2iC"   => TwoIC,
            "3iC"   => ThreeIC,
            "NCOiC" => NCOIC,
            _       => null
        };

    public void SetMemberAtPosition(string position, string? memberId)
    {
        switch (position)
        {
            case "1iC":   OneIC = memberId; break;
            case "2iC":   TwoIC = memberId; break;
            case "3iC":   ThreeIC = memberId; break;
            case "NCOiC": NCOIC = memberId; break;
        }
    }

    public bool HasPosition(string position) => !string.IsNullOrEmpty(GetMemberAtPosition(position));

    public int GetPositionOrder(string position) =>
        position switch
        {
            "1iC"   => 0,
            "2iC"   => 1,
            "3iC"   => 2,
            "NCOiC" => 3,
            _       => int.MaxValue
        };

    public IEnumerable<(string Position, string MemberId)> GetAssignedPositions()
    {
        var positions = new List<(string, string)>();
        if (!string.IsNullOrEmpty(OneIC)) positions.Add(("1iC", OneIC));
        if (!string.IsNullOrEmpty(TwoIC)) positions.Add(("2iC", TwoIC));
        if (!string.IsNullOrEmpty(ThreeIC)) positions.Add(("3iC", ThreeIC));
        if (!string.IsNullOrEmpty(NCOIC)) positions.Add(("NCOiC", NCOIC));
        return positions;
    }
}
