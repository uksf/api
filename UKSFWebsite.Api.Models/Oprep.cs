using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models {
    public class Oprep {
        public AttendanceReport attendanceReport;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string name, map, type, result, description;
        public DateTime start, end;
    }
}
