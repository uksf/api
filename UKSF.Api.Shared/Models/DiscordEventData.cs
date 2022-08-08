namespace UKSF.Api.Shared.Models;

public class DiscordEventData
{
    public string EventData { get; set; }
    public DiscordUserEventType EventType { get; set; }

    public DiscordEventData(DiscordUserEventType eventType, string eventData)
    {
        EventType = eventType;
        EventData = eventData;
    }
}
