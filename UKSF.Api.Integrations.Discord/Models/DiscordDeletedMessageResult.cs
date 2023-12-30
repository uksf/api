namespace UKSF.Api.Integrations.Discord.Models;

public class DiscordDeletedMessageResult
{
    public DiscordDeletedMessageResult(ulong instigatorId, string instigatorName, string name, string message)
    {
        InstigatorId = instigatorId;
        InstigatorName = instigatorName;
        Name = name;
        Message = message;
    }

    public ulong InstigatorId { get; set; }
    public string InstigatorName { get; set; }
    public string Message { get; set; }
    public string Name { get; set; }
}
