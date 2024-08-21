using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Models;

public enum DiscordUserEventType
{
    Joined,
    Left,
    Banned,
    Unbanned,
    Message_Deleted
}

public class DiscordLog : DomainBasicLog
{
    public DiscordLog(
        DiscordUserEventType discordUserEventType,
        string instigatorId,
        string instigatorName,
        string channelName,
        string name,
        string message
    ) : base(message)
    {
        DiscordUserEventType = discordUserEventType;
        InstigatorId = instigatorId;
        InstigatorName = instigatorName;
        ChannelName = channelName;
        Name = name;
    }

    public string ChannelName { get; set; }

    [BsonRepresentation(BsonType.String)]
    public DiscordUserEventType DiscordUserEventType { get; set; }

    public string InstigatorId { get; set; }
    public string InstigatorName { get; set; }
    public string Name { get; set; }
}
