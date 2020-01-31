using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Message {
    public enum ThreadMode {
        ALL,
        SR1,
        RANKSUPERIOR,
        RANKEQUAL,
        RANKSUPERIOROREQUAL
    }

    public class CommentThread : MongoObject {
        [BsonRepresentation(BsonType.ObjectId)] public string[] authors;
        public Comment[] comments = { };
        public ThreadMode mode;
    }
}
