using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UKSF.Api.Interfaces.Events;

namespace UKSF.Api.Events {
    public class EventBus<T> : IEventBus<T> {
        protected readonly Subject<object> Subject = new Subject<object>();

        public void Send(T message) {
            Subject.OnNext(message);
        }

        public virtual IObservable<T> AsObservable() => Subject.OfType<T>();
    }
}
