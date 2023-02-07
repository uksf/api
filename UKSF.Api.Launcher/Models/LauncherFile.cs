using UKSF.Api.Core.Models;

namespace UKSF.Api.Launcher.Models;

public class LauncherFile : MongoObject
{
    public string FileName { get; set; }
    public string Version { get; set; }
}
