using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Models;

public class AuditLog : DomainBasicLog
{
    public AuditLog(string who, string message) : base(message)
    {
        Who = who;
        Level = UksfLogLevel.Info;
    }

    public string Who { get; set; }
}
