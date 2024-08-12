using System.Reactive.Linq;
using System.Reactive.Subjects;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Events;

public interface IEventBus
{
    void Send(EventModel eventModel);
    void Send(EventData data, string eventSource);
    IObservable<EventModel> AsObservable();
}

public class EventBus : IEventBus
{
    private readonly Subject<object> _subject = new();

    public void Send(EventModel eventModel)
    {
        _subject.OnNext(eventModel);
    }

    public void Send(EventData data, string eventSource)
    {
        Send(new EventModel(EventType.None, data, eventSource));
    }

    public IObservable<EventModel> AsObservable()
    {
        return _subject.OfType<EventModel>();
    }
}
