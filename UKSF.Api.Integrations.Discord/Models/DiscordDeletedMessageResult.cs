namespace UKSF.Api.Discord.Models {
    public class DiscordDeletedMessageResult {
        public readonly ulong InstigatorId;
        public readonly string InstigatorName;
        public readonly string Name;
        public readonly string Message;

        public DiscordDeletedMessageResult(ulong instigatorId, string instigatorName, string name, string message) {
            InstigatorId = instigatorId;
            InstigatorName = instigatorName;
            Name = name;
            Message = message;
        }
    }
}
