namespace UKSF.Api.Shared.Models
{
    public class DiscordEventData
    {
        public string EventData;
        public DiscordUserEventType EventType;

        public DiscordEventData(DiscordUserEventType eventType, string eventData)
        {
            EventType = eventType;
            EventData = eventData;
        }
    }
}
