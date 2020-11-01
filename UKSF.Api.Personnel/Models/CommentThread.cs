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

    public class CommentThread : DatabaseObject {
        [BsonRepresentation(BsonType.ObjectId)] public string[] authors;
        public Comment[] comments = { };
        public ThreadMode mode;
    }
}
