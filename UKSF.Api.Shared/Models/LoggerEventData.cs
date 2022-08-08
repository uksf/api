namespace UKSF.Api.Shared.Models;

public class LoggerEventData
{
    public BasicLog Log { get; set; }

    public LoggerEventData(BasicLog log)
    {
        Log = log;
    }
}
