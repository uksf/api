using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Events {
    public interface ISignalrEventBus : IEventBus<SignalrEventModel> { }

    public class SignalrEventBus : EventBus<SignalrEventModel>, ISignalrEventBus {
//        public IObservable<SignalrEventModel> AsObservable(string clientName) => Subject.OfType<SignalrEventModel>();
    }
}
