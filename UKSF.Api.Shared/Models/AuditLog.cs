namespace UKSF.Api.Shared.Models {
    public class AuditLog : BasicLog {
        public string who;

        public AuditLog(string who, string message) : base(message) {
            this.who = who;
            level = LogLevel.INFO;
        }
    }
}
