using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Models.Operations {
    public class Operation {
        public AttendanceReport attendanceReport;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public string name, map, type, result;
        public DateTime start, end;
    }
}
