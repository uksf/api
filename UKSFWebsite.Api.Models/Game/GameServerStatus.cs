using System;
using System.Collections.Generic;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Models.Game {
    public class GameServerStatus {
        public DateTime timestamp;
        public int port;
        public GameServerType type = GameServerType.SERVER;
        public string name;
        public int processId = 0;
        public int state = 0;
        public string map;
        public string mission;
        public float uptime = 0;
        public float missionUptime = 0;
        public int playerCount;
        public Dictionary<string, GamePlayerUpdate> playerMap = new Dictionary<string, GamePlayerUpdate>();

        public string Key() => $"{port}:{type.Value()}:{name}";
    }
}
