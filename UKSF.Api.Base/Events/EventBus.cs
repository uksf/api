using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace UKSF.Api.Base.Events {
    public interface IEventBus<T> {
        void Send(T message);
        IObservable<T> AsObservable();
    }

    public class EventBus<T> : IEventBus<T> {
        protected readonly Subject<object> Subject = new();

        public void Send(T message) {
            Subject.OnNext(message);
        }

        public virtual IObservable<T> AsObservable() => Subject.OfType<T>();
    }
}
