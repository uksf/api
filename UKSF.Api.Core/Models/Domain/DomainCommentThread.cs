using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public enum ThreadMode
{
    All,
    Recruiter,
    Ranksuperior,
    Rankequal,
    Ranksuperiororequal
}

public class DomainCommentThread : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string[] Authors { get; set; }

    public DomainComment[] Comments { get; set; } = [];
    public ThreadMode Mode { get; set; }
}
