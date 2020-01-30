using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Events.SignalrServer {
    public class SignalrEventBus : EventBus<SignalrEventModel>, ISignalrEventBus {
//        public IObservable<SignalrEventModel> AsObservable(string clientName) => Subject.OfType<SignalrEventModel>();
    }
}
