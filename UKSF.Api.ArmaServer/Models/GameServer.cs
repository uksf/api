using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.ArmaServer.Models {
    public enum GameServerOption {
        NONE,
        SINGLETON,
        DCG
    }

    public record GameServer : MongoObject {
        [BsonIgnore] public List<int> HeadlessClientProcessIds { get; set; } = new();
        public string AdminPassword { get; set; }
        public int ApiPort { get; set; }
        [BsonIgnore] public bool CanLaunch { get; set; }
        public GameEnvironment Environment { get; set; }
        public string HostName { get; set; }
        public List<GameServerMod> Mods { get; set; } = new();
        public string Name { get; set; }
        public int NumberHeadlessClients { get; set; }
        public int Order { get; set; } = 0;
        public string Password { get; set; }
        public int Port { get; set; }
        [BsonIgnore] public int? ProcessId { get; set; }
        public string ProfileName { get; set; }
        public List<GameServerMod> ServerMods { get; set; } = new();
        public GameServerOption ServerOption { get; set; }
        [BsonIgnore] public GameServerStatus Status { get; set; }= new();

        public override string ToString() => $"{Name}, {Port}, {ApiPort}, {NumberHeadlessClients}, {ProfileName}, {HostName}, {Password}, {AdminPassword}, {Environment}, {ServerOption}";
    }

    public class GameServerStatus {
        public string Map { get; set; }
        public string MaxPlayers { get; set; }
        public string Mission { get; set; }
        public string ParsedUptime { get; set; }
        public int Players { get; set; }
        public bool Running { get; set; }
        public bool Started { get; set; }
        public float Uptime { get; set; }
    }
}
