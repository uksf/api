namespace UKSFWebsite.Api.Models.Logging {
    public class LauncherLogMessage : BasicLogMessage {
        public string userId;
        public string name;
        public string version;

        public LauncherLogMessage(string version, string message) : base(message) => this.version = version;
    }
}
