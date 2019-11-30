using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.SignalrServer {
    public class TeamspeakEventBus : EventBus<TeamspeakEventModel>, ITeamspeakEventBus {
//        public IObservable<SignalrEventModel> AsObservable(string clientName) => Subject.OfType<SignalrEventModel>();
    }
}
