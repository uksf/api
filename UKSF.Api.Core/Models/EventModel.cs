namespace UKSF.Api.Core.Models;

public enum EventType
{
    NONE,
    ADD,
    UPDATE,
    DELETE
}

public class EventModel
{
    public EventModel(EventType eventType, object data)
    {
        EventType = eventType;
        Data = data;
    }

    public object Data { get; set; }
    public EventType EventType { get; set; }
}
