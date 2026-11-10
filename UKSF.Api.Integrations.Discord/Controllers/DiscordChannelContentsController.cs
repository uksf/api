using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Integrations.Discord.Models;
using UKSF.Api.Integrations.Discord.Services;

namespace UKSF.Api.Integrations.Discord.Controllers;

[Route("discord/channelContents")]
[Permissions(Permissions.Admin)]
public class DiscordChannelContentsController(IDiscordChannelContentService discordChannelContentService) : ControllerBase
{
    [HttpGet]
    public IEnumerable<DomainDiscordChannelContent> GetAll()
    {
        return discordChannelContentService.GetAll();
    }

    [HttpPost]
    public Task<DomainDiscordChannelContent> Create([FromBody] DiscordChannelContentRequest request)
    {
        return discordChannelContentService.Create(request);
    }

    [HttpPut("{id}")]
    public Task<DomainDiscordChannelContent> Update([FromRoute] string id, [FromBody] DiscordChannelContentRequest request)
    {
        return discordChannelContentService.Update(id, request);
    }

    [HttpPost("{id}/publish")]
    public Task<DomainDiscordChannelContent> Publish([FromRoute] string id)
    {
        return discordChannelContentService.Publish(id);
    }
}
