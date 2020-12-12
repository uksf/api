namespace UKSF.Api.Shared.Models {
    public class AuditLog : BasicLog {
        public string Who;

        public AuditLog(string who, string message) : base(message) {
            Who = who;
            Level = LogLevel.INFO;
        }
    }
}
