using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.SignalrServer {
    public class SignalrEventBus : EventBus<SignalrEventModel>, ISignalrEventBus {
//        public IObservable<SignalrEventModel> AsObservable(string clientName) => Subject.OfType<SignalrEventModel>();
    }
}
