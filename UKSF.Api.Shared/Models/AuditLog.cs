namespace UKSF.Api.Shared.Models;

public class AuditLog : BasicLog
{
    public string Who { get; set; }

    public AuditLog(string who, string message) : base(message)
    {
        Who = who;
        Level = LogLevel.INFO;
    }
}
