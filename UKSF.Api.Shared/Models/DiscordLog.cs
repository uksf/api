namespace UKSF.Api.Shared.Models {
    public enum DiscordUserEventType {
        JOINED,
        LEFT,
        BANNED,
        UNBANNED
    }

    public record DiscordLog : BasicLog {
        public DiscordLog(DiscordUserEventType discordUserEventType, string name, string userId, string message) : base(message) {
            UserId = userId;
            Name = name;
            DiscordUserEventType = discordUserEventType;
        }

        public string UserId { get; }
        public string Name { get; }
        public DiscordUserEventType DiscordUserEventType { get; }
    }
}
