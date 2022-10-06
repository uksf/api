namespace UKSF.Api.ArmaServer.Models;

public class ServerInfrastructureLatest
{
    public string LatestBuild { get; set; }
    public DateTime LatestUpdate { get; set; }
}

public class ServerInfrastructureCurrent
{
    public string CurrentBuild { get; set; }
    public DateTime CurrentUpdated { get; set; }
}

public class ServerInfrastructureInstalled
{
    public string InstalledVersion { get; set; }
}

public class ServerInfrastructureUpdate
{
    public string NewVersion { get; set; }
    public string UpdateOutput { get; set; }
}
