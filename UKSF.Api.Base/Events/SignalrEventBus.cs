using UKSF.Api.Base.Models;

namespace UKSF.Api.Base.Events {
    public interface ISignalrEventBus : IEventBus<SignalrEventModel> { }

    public class SignalrEventBus : EventBus<SignalrEventModel>, ISignalrEventBus {
//        public IObservable<SignalrEventModel> AsObservable(string clientName) => Subject.OfType<SignalrEventModel>();
    }
}
