using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public enum GameServerOption
{
    None,
    Singleton,
    Dcg
}

public class DomainGameServer : MongoObject
{
    public List<GameServerMod> Mods { get; set; } = [];
    public string AdminPassword { get; set; }
    public int ApiPort { get; set; }

    public GameEnvironment Environment { get; set; }
    public List<int> HeadlessClientProcessIds { get; set; } = new();
    public string HostName { get; set; }
    public string Name { get; set; }
    public int NumberHeadlessClients { get; set; }
    public int Order { get; set; } = 0;
    public string Password { get; set; }
    public int Port { get; set; }
    public int? ProcessId { get; set; }
    public string ProfileName { get; set; }
    public List<GameServerMod> ServerMods { get; set; } = [];
    public GameServerOption ServerOption { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string LaunchedBy { get; set; }

    [BsonIgnore]
    public GameServerStatus Status { get; set; } = new();

    public override string ToString()
    {
        return $"{Name}, {Port}, {ApiPort}, {NumberHeadlessClients}, {ProfileName}, {HostName}, {Password}, {AdminPassword}, {Environment}, {ServerOption}";
    }
}

public class GameServerStatus
{
    public string Map { get; set; }
    public string MaxPlayers { get; set; }
    public string Mission { get; set; }
    public string ParsedUptime { get; set; }
    public int Players { get; set; }
    public bool Running { get; set; }
    public bool Started { get; set; }
    public float Uptime { get; set; }
}
