using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum OperationStatus
{
    Upcoming = 0,
    Current = 1,
    Past = 2
}

public class DomainOperation : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string CampaignId { get; set; }

    public string Title { get; set; }
    public string Brief { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Upcoming;
}
