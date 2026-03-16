namespace UKSF.Api.ArmaServer.Models;

public class GameServersUpdate
{
    public List<DomainGameServer> Servers { get; set; }
    public int InstanceCount { get; set; }
    public List<MissionFile> Missions { get; set; }
}

public class GameServerUpdate
{
    public DomainGameServer Server { get; set; }
    public int InstanceCount { get; set; }
}
