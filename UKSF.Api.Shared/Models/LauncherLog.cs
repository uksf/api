namespace UKSF.Api.Shared.Models {
    public class LauncherLog : BasicLog {
        public string name;
        public string userId;
        public string version;

        public LauncherLog(string version, string message) : base(message) => this.version = version;
    }
}
