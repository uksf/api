namespace UKSF.Api.Core.Models;

public class ScheduledJob : MongoObject
{
    public string Action { get; set; }
    public string ActionParameters { get; set; }
    public TimeSpan Interval { get; set; }
    public DateTime Next { get; set; }
    public bool Repeat { get; set; }
}
