namespace UKSF.Api.Shared.Models {
    public record LauncherLog : BasicLog {
        public string Name { get; set; }
        public string UserId { get; set; }
        public string Version { get; set; }

        public LauncherLog(string version, string message) : base(message) => Version = version;
    }
}
