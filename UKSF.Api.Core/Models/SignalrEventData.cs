namespace UKSF.Api.Core.Models;

public class SignalrEventData
{
    public object Args { get; set; }
    public TeamspeakEventType Procedure { get; set; }
}
