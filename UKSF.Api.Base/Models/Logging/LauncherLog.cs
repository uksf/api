namespace UKSF.Api.Base.Models.Logging {
    public class LauncherLog : BasicLog {
        public string name;
        public string userId;
        public string version;

        public LauncherLog(string version, string message) : base(message) => this.version = version;
    }
}
