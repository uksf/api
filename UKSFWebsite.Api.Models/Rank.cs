using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models {
    public class Rank {
        public string abbreviation;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string name;
        public int order = 0;
        public string teamspeakGroup;
        public string discordRoleId;
    }
}
