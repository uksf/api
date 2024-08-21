using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public class DomainDischargeCollection : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string AccountId { get; set; }

    public List<DomainDischarge> Discharges { get; set; } = new();
    public string Name { get; set; }
    public bool Reinstated { get; set; }

    [BsonIgnore]
    public bool RequestExists { get; set; }
}

public class DomainDischarge : MongoObject
{
    public string DischargedBy { get; set; }
    public string Rank { get; set; }
    public string Reason { get; set; }
    public string Role { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Unit { get; set; }
}
