using UKSF.Api.Shared.Models;

namespace UKSF.Api.Teamspeak.Models;

public class Operation : MongoObject
{
    public AttendanceReport AttendanceReport { get; set; }
    public DateTime End { get; set; }
    public string Map { get; set; }
    public string Name { get; set; }
    public string Result { get; set; }
    public DateTime Start { get; set; }
    public string Type { get; set; }
}
