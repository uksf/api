using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Events;

namespace UKSFWebsite.Api.Events.Data {
    public class DataEventBus : IEventBus {
        private readonly Subject<object> subject = new Subject<object>();

        public void Send<T>(T message) {
            Task.Run(() => subject.OnNext(message));
        }

        public IObservable<T> AsObservable<T>() => subject.OfType<T>();
    }
}
