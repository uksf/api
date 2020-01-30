using System;

namespace UKSF.Api.Interfaces.Events {
    public interface IEventBus<T> {
        void Send(T message);
        IObservable<T> AsObservable();
    }
}
