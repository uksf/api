using System;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Interfaces.Events {
    public interface ISocketEventBus : IEventBus<SocketEventModel> {
        IObservable<SocketEventModel> AsObservable(string clientName);
    }
}
