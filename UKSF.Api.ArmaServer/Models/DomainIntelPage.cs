using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum IntelScope
{
    Campaign = 0,
    Operation = 1,
    Mission = 2
}

public class DomainIntelPage : MongoObject
{
    public IntelScope Scope { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerId { get; set; }

    public string Title { get; set; }
    public string Body { get; set; }
}
