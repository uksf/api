namespace UKSF.Api.Models.Message.Logging {
    public class LauncherLogMessage : BasicLogMessage {
        public string name;
        public string userId;
        public string version;

        public LauncherLogMessage(string version, string message) : base(message) => this.version = version;
    }
}
