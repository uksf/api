using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Events;

namespace UKSFWebsite.Api.Events {
    public class EventBus<T> : IEventBus<T> {
        protected readonly Subject<object> Subject = new Subject<object>();

        public void Send(T message) {
            Task.Run(() => Subject.OnNext(message));
        }

        public virtual IObservable<T> AsObservable() => Subject.OfType<T>();
    }
}
