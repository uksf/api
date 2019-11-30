using UKSFWebsite.Api.Models.Events.Types;
using UKSFWebsite.Api.Models.Game;

namespace UKSFWebsite.Api.Models.Events {
    public class GameEventModel {
        public GameServerType server;
        public GameEventType procedure;
        public object args;
    }
}
