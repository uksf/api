namespace UKSF.Api.Shared.Models;

public class LauncherLog : BasicLog
{
    public LauncherLog(string version, string message) : base(message)
    {
        Version = version;
    }

    public string Name { get; set; }
    public string UserId { get; set; }
    public string Version { get; set; }
}
