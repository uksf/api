using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSFWebsite.Api.Models.Game {
    public enum GameServerOption {
        NONE,
        SINGLETON,
        DCG
    }

    public class GameServer {
        [BsonIgnore] public readonly List<uint> headlessClientProcessIds = new List<uint>();
        public string adminPassword;
        public int apiPort;
        [BsonIgnore] public bool canLaunch;
        public string hostName;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public List<GameServerMod> mods = new List<GameServerMod>();
        public string name;
        public int numberHeadlessClients;
        public int order = 0;
        public string password;
        public int port;
        [BsonIgnore] public uint? processId;
        public string profileName;
        public string serverMods;
        public GameServerOption serverOption;
        [BsonIgnore] public GameServerStatus status = new GameServerStatus();

        public override string ToString() => $"{name}, {port}, {apiPort}, {numberHeadlessClients}, {profileName}, {hostName}, {password}, {adminPassword}, {serverOption}, {serverMods}";
    }

    public class GameServerStatus {
        public string map;
        public string maxPlayers;
        public string mission;
        public string parsedUptime;
        public int players;
        public bool running;
        public bool started;
        public float uptime;
    }
}