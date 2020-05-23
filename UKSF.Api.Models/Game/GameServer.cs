using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Game {
    public enum GameServerOption {
        NONE,
        SINGLETON,
        DCG
    }

    public class GameServer : DatabaseObject {
        [BsonIgnore] public readonly List<int> headlessClientProcessIds = new List<int>();
        public string adminPassword;
        public int apiPort;
        [BsonIgnore] public bool canLaunch;
        public string hostName;
        public List<GameServerMod> mods = new List<GameServerMod>();
        public string name;
        public int numberHeadlessClients;
        public int order = 0;
        public string password;
        public int port;
        [BsonIgnore] public int? processId;
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
