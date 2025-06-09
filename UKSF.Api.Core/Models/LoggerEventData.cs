using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Models;

public class LoggerEventData(DomainBasicLog log) : EventData
{
    public DomainBasicLog Log { get; } = log;
}
