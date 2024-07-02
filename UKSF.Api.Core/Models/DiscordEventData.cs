namespace UKSF.Api.Core.Models;

public class DiscordEventData(DiscordUserEventType eventType, string eventData) : EventData
{
    public string EventData { get; } = eventData;
    public DiscordUserEventType EventType { get; } = eventType;
}
