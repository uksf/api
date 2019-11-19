using UKSFWebsite.Api.Models.Events.Types;

namespace UKSFWebsite.Api.Models.Events {
    public class SignalrEventModel {
        public TeamspeakEventType procedure;
        public object args;
    }
}
