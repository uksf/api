using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Personnel {
    public class Rank {
        public string abbreviation;
        public string discordRoleId;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string name;
        public int order = 0;
        public string teamspeakGroup;
    }
}
