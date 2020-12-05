using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public enum ThreadMode {
        ALL,
        RECRUITER,
        RANKSUPERIOR,
        RANKEQUAL,
        RANKSUPERIOROREQUAL
    }

    public record CommentThread : MongoObject {
        [BsonRepresentation(BsonType.ObjectId)] public string[] Authors { get; set; }
        public Comment[] Comments { get; set; } = System.Array.Empty<Comment>();
        public ThreadMode Mode { get; set; }
    }
}
