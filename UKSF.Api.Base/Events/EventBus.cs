using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Base.Events {
    public interface IEventBus {
        void Send(EventModel eventModel);
        void Send(object data);
        IObservable<EventModel> AsObservable();
    }

    public class EventBus : IEventBus {
        protected readonly Subject<object> Subject = new();

        public void Send(EventModel eventModel) {
            Subject.OnNext(eventModel);
        }

        public void Send(object data) {
            Send(new(EventType.NONE, data));
        }

        public virtual IObservable<EventModel> AsObservable() => Subject.OfType<EventModel>();
    }
}
