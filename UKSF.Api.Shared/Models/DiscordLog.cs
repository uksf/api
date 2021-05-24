using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Shared.Models
{
    public enum DiscordUserEventType
    {
        JOINED,
        LEFT,
        BANNED,
        UNBANNED,
        MESSAGE_DELETED
    }

    public class DiscordLog : BasicLog
    {
        public string ChannelName;

        [BsonRepresentation(BsonType.String)] public DiscordUserEventType DiscordUserEventType;

        public string InstigatorId;
        public string InstigatorName;
        public string Name;

        public DiscordLog(DiscordUserEventType discordUserEventType, string instigatorId, string instigatorName, string channelName, string name, string message) : base(message)
        {
            DiscordUserEventType = discordUserEventType;
            InstigatorId = instigatorId;
            InstigatorName = instigatorName;
            ChannelName = channelName;
            Name = name;
        }
    }
}
