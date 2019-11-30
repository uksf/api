using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Models.Game {
    public enum GameServerOption {
        NONE,
        SINGLETON,
        DCG
    }

    public class GameServer {
        public string adminPassword;
        public string hostName;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public List<GameServerMod> mods = new List<GameServerMod>();
        public string name;
        public int numberHeadlessClients;
        public int order = 0;
        public string password;
        public int port;
        public string profileName;
        public string serverMods;
        public GameServerOption serverOption = GameServerOption.NONE;

        public override string ToString() => $"{name}, {port}, {numberHeadlessClients}, {profileName}, {hostName}, {password}, {adminPassword}, {serverOption}, {serverMods}";
        public string Key() => $"{port}:{GameServerType.SERVER.Value()}:{name}";
        public string HeadlessClientKey(string headlessClientName) => $"{port}:{GameServerType.HEADLESS}:{headlessClientName}";
    }
}
