namespace UKSF.Api.ArmaServer.Models;

public class GameServersDataset
{
    public int InstanceCount { get; set; }
    public List<MissionFile> Missions { get; set; }
    public IEnumerable<DomainGameServer> Servers { get; set; }
}

public class GameServerDataset
{
    public DomainGameServer GameServer { get; set; }
    public int InstanceCount { get; set; }
}
