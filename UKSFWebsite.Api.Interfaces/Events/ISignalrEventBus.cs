using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Interfaces.Events {
    public interface ISignalrEventBus : IEventBus<SignalrEventModel> { }
}
