namespace UKSF.Api.Base.Models
{
    public enum EventType
    {
        NONE,
        ADD,
        UPDATE,
        DELETE
    }

    public class EventModel
    {
        public object Data;
        public EventType EventType;

        public EventModel(EventType eventType, object data)
        {
            EventType = eventType;
            Data = data;
        }
    }
}
