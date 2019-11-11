using System;

namespace UKSFWebsite.Api.Interfaces.Events {
    public interface IEventBus {
        void Send<T>(T message);
        IObservable<T> AsObservable<T>();
    }
}
