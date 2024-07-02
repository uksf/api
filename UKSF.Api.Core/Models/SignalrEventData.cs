namespace UKSF.Api.Core.Models;

public class SignalrEventData : EventData
{
    public object Args { get; set; }
    public TeamspeakEventType Procedure { get; set; }
}
