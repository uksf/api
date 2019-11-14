using System;
using System.Reactive.Linq;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.SocketServer {
    public class SocketEventBus : EventBus<SocketEventModel>, ISocketEventBus {
        public IObservable<SocketEventModel> AsObservable(string clientName) => Subject.OfType<SocketEventModel>().Where(x => x.clientName == clientName);
    }
}
