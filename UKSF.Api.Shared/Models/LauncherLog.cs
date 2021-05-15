namespace UKSF.Api.Shared.Models
{
    public class LauncherLog : BasicLog
    {
        public string Name;
        public string UserId;
        public string Version;

        public LauncherLog(string version, string message) : base(message)
        {
            Version = version;
        }
    }
}
