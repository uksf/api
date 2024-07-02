namespace UKSF.Api.Core.Models;

public enum EventType
{
    None,
    Add,
    Update,
    Delete
}

public class EventModel
{
    public EventModel(EventType eventType, EventData data)
    {
        Data = data;
        EventType = eventType;
    }

    public EventData Data { get; }
    public EventType EventType { get; }
}

public class EventData { }
