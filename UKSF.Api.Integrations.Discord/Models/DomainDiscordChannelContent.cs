using UKSF.Api.Core.Models;

namespace UKSF.Api.Integrations.Discord.Models;

public class DomainDiscordChannelContent : MongoObject
{
    public string ChannelId { get; set; }
    public string Name { get; set; }
    public string Content { get; set; } = "";
    public List<string> MessageIds { get; set; } = [];
    public DateTime? PublishedAt { get; set; }
}

public record DiscordChannelContentRequest(string ChannelId, string Name, string Content);
