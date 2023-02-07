namespace UKSF.Api.Core.Models;

public class LoggerEventData
{
    public LoggerEventData(BasicLog log)
    {
        Log = log;
    }

    public BasicLog Log { get; set; }
}
