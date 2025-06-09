using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Models;

public class LauncherLog : DomainBasicLog
{
    public LauncherLog(string version, string message) : base(message)
    {
        Version = version;
    }

    public string Name { get; set; }
    public string UserId { get; set; }
    public string Version { get; set; }
}
