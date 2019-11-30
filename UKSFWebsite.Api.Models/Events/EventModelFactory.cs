using UKSFWebsite.Api.Models.Events.Types;
using UKSFWebsite.Api.Models.Game;

namespace UKSFWebsite.Api.Models.Events {
    public static class EventModelFactory {
        public static DataEventModel<TData> CreateDataEvent<TData>(DataEventType type, string id, object data = null) => new DataEventModel<TData> {type = type, id = id, data = data};
        public static TeamspeakEventModel CreateTeamspeakEvent(TeamspeakEventType procedure, object args) => new TeamspeakEventModel {procedure = procedure, args = args};
        public static GameEventModel CreateGameEvent(GameServerType server, GameEventType procedure, object args) => new GameEventModel {server = server, procedure = procedure, args = args};
    }
}
