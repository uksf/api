using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Models.Operations {
    public class Oprep {
        public AttendanceReport attendanceReport;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string name, map, type, result, description;
        public DateTime start, end;
    }
}
