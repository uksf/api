namespace UKSF.Api.Core.Models;

public enum EventType
{
    None,
    Add,
    Update,
    Delete
}

public record EventModel(EventType EventType, EventData Data, string EventSource)
{
    public EventData Data { get; } = Data;
    public EventType EventType { get; } = EventType;
    public string EventSource { get; } = EventSource;
}

public class EventData;
