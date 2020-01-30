using UKSF.Api.Models.Events.Types;

namespace UKSF.Api.Models.Events {
    public class SignalrEventModel {
        public TeamspeakEventType procedure;
        public object args;
    }
}
