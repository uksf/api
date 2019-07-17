using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models {
    public enum RoleType {
        INDIVIDUAL,
        UNIT
    }

    public class Role {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string name;
        public int order = 0;
        public RoleType roleType = RoleType.INDIVIDUAL;
    }
}
