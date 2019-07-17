using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models.Launcher {
    public class LauncherFile {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string fileName;
        public string version;
    }
}
