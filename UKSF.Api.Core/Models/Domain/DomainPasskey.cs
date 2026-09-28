using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public class DomainPasskey : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string AccountId { get; set; }

    public byte[] CredentialId { get; set; }
    public byte[] PublicKey { get; set; }
    public byte[] UserHandle { get; set; }
    public long SignCount { get; set; }
    public List<string> Transports { get; set; } = [];
    public string AaGuid { get; set; }
    public string Name { get; set; }
    public bool IsBackedUp { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastUsed { get; set; }
}
