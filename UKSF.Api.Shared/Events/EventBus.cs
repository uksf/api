using System.Reactive.Linq;
using System.Reactive.Subjects;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Events;

public interface IEventBus
{
    void Send(EventModel eventModel);
    void Send(object data);
    IObservable<EventModel> AsObservable();
}

public class EventBus : IEventBus
{
    private readonly Subject<object> _subject = new();

    public void Send(EventModel eventModel)
    {
        _subject.OnNext(eventModel);
    }

    public void Send(object data)
    {
        Send(new(EventType.NONE, data));
    }

    public IObservable<EventModel> AsObservable()
    {
        return _subject.OfType<EventModel>();
    }
}
