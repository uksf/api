namespace UKSF.Api.Core.Models;

public class DiscordEventData
{
    public DiscordEventData(DiscordUserEventType eventType, string eventData)
    {
        EventType = eventType;
        EventData = eventData;
    }

    public string EventData { get; set; }
    public DiscordUserEventType EventType { get; set; }
}
