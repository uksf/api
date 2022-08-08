namespace UKSF.Api.Base.Models;

public enum EventType
{
    NONE,
    ADD,
    UPDATE,
    DELETE
}

public class EventModel
{
    public object Data { get; set; }
    public EventType EventType { get; set; }

    public EventModel(EventType eventType, object data)
    {
        EventType = eventType;
        Data = data;
    }
}
