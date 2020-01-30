using UKSF.Api.Models.Events;

namespace UKSF.Api.Interfaces.Events {
    public interface ISignalrEventBus : IEventBus<SignalrEventModel> { }
}
