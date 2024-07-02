namespace UKSF.Api.Core.Models;

public class LoggerEventData(BasicLog log) : EventData
{
    public BasicLog Log { get; } = log;
}
