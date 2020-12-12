using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.ArmaServer.Models {
    public enum GameServerOption {
        NONE,
        SINGLETON,
        DCG
    }

    public class GameServer : MongoObject {
        [BsonIgnore] public List<int> HeadlessClientProcessIds = new();
        public string AdminPassword;
        public int ApiPort;
        [BsonIgnore] public bool CanLaunch;
        public GameEnvironment Environment;
        public string HostName;
        public List<GameServerMod> Mods = new();
        public string Name;
        public int NumberHeadlessClients;
        public int Order = 0;
        public string Password;
        public int Port;
        [BsonIgnore] public int? ProcessId;
        public string ProfileName;
        public List<GameServerMod> ServerMods = new();
        public GameServerOption ServerOption;
        [BsonIgnore] public GameServerStatus Status = new();

        public override string ToString() => $"{Name}, {Port}, {ApiPort}, {NumberHeadlessClients}, {ProfileName}, {HostName}, {Password}, {AdminPassword}, {Environment}, {ServerOption}";
    }

    public class GameServerStatus {
        public string Map;
        public string MaxPlayers;
        public string Mission;
        public string ParsedUptime;
        public int Players;
        public bool Running;
        public bool Started;
        public float Uptime;
    }
}
