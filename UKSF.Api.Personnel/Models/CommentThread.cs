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
        [BsonRepresentation(BsonType.ObjectId)] public string[] Authors;
        public Comment[] Comments = { };
        public ThreadMode Mode;
    }
}
