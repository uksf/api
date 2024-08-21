using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models;

public class AccountAttendanceStatus
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string AccountId { get; set; }

    public float AttendancePercent { get; set; }
    public AttendanceState AttendanceState { get; set; }
    public string DisplayName { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string GroupId { get; set; }

    public string GroupName { get; set; }
}

public enum AttendanceState
{
    Full,
    Partial,
    Mia,
    Awol,
    Loa
}
