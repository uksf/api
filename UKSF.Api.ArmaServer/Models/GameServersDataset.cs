using System.Collections.Generic;

namespace UKSF.Api.ArmaServer.Models
{
    public class GameServersDataset
    {
        public int InstanceCount;
        public List<MissionFile> Missions;
        public IEnumerable<GameServer> Servers;
    }

    public class GameServerDataset
    {
        public GameServer GameServer;
        public int InstanceCount;
    }
}
