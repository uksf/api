namespace UKSF.Api.Core.Models;

public class AuditLog : BasicLog
{
    public AuditLog(string who, string message) : base(message)
    {
        Who = who;
        Level = UksfLogLevel.INFO;
    }

    public string Who { get; set; }
}
