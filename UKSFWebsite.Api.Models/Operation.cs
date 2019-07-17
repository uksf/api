using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models {
    public class Operation {
        public AttendanceReport attendanceReport;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string name, map, type, result;
        public DateTime start, end;
    }
}
