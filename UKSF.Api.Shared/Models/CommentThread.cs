using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Shared.Models;

public enum ThreadMode
{
    ALL,
    RECRUITER,
    RANKSUPERIOR,
    RANKEQUAL,
    RANKSUPERIOROREQUAL
}

public class CommentThread : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string[] Authors { get; set; }

    public Comment[] Comments { get; set; } = Array.Empty<Comment>();
    public ThreadMode Mode { get; set; }
}
