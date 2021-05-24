using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Personnel.Models
{
    public class AccountAttendanceStatus
    {
        [BsonRepresentation(BsonType.ObjectId)] public string AccountId;
        public float AttendancePercent;
        public AttendanceState AttendanceState;
        public string DisplayName;
        [BsonRepresentation(BsonType.ObjectId)] public string GroupId;
        public string GroupName;
    }

    public enum AttendanceState
    {
        FULL,
        PARTIAL,
        MIA,
        AWOL,
        LOA
    }
}
