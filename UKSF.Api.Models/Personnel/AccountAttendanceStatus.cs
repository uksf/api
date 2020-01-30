using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Personnel {
    public class AccountAttendanceStatus {
        [BsonRepresentation(BsonType.ObjectId)] public string accountId;
        public float attendancePercent;
        public AttendanceState attendanceState;
        public string displayName;
        [BsonRepresentation(BsonType.ObjectId)] public string groupId;
        public string groupName;
    }

    public enum AttendanceState {
        FULL,
        PARTIAL,
        MIA,
        AWOL,
        LOA
    }
}
