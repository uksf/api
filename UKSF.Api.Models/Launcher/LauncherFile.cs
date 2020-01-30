using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Launcher {
    public class LauncherFile {
        public string fileName;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string version;
    }
}
