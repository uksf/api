using Discord;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Integrations.Discord.Context;
using UKSF.Api.Integrations.Discord.Models;

namespace UKSF.Api.Integrations.Discord.Services;

public interface IDiscordChannelContentService
{
    IEnumerable<DomainDiscordChannelContent> GetAll();
    Task<DomainDiscordChannelContent> Create(DiscordChannelContentRequest request);
    Task<DomainDiscordChannelContent> Update(string id, DiscordChannelContentRequest request);
    Task<DomainDiscordChannelContent> Publish(string id);
}

public class DiscordChannelContentService(IDiscordChannelContentsContext context, IDiscordClientService discordClientService) : IDiscordChannelContentService
{
    public const string MessageSeparator = "---";

    public IEnumerable<DomainDiscordChannelContent> GetAll()
    {
        return context.Get().OrderBy(x => x.Name);
    }

    public async Task<DomainDiscordChannelContent> Create(DiscordChannelContentRequest request)
    {
        Validate(request);
        if (context.GetSingle(x => x.ChannelId == request.ChannelId) is not null)
        {
            throw new BadRequestException($"Channel {request.ChannelId} already has managed content");
        }

        var item = new DomainDiscordChannelContent
        {
            ChannelId = request.ChannelId,
            Name = request.Name,
            Content = request.Content
        };
        await context.Add(item);
        return item;
    }

    public async Task<DomainDiscordChannelContent> Update(string id, DiscordChannelContentRequest request)
    {
        Validate(request);
        var item = GetExisting(id);
        if (item.ChannelId != request.ChannelId)
        {
            throw new BadRequestException("The channel of published content cannot change");
        }

        item.Name = request.Name;
        item.Content = request.Content;
        await context.Replace(item);
        return item;
    }

    public async Task<DomainDiscordChannelContent> Publish(string id)
    {
        var item = GetExisting(id);
        if (discordClientService.IsDiscordDisabled())
        {
            throw new BadRequestException("Discord is disabled");
        }

        await discordClientService.AssertOnline();
        var channel = discordClientService.GetGuild().GetTextChannel(ulong.Parse(item.ChannelId)) ??
                      throw new BadRequestException($"Text channel {item.ChannelId} was not found");

        var messageIds = await Sync(channel, item.MessageIds.Select(ulong.Parse).ToList(), Split(item.Content));

        item.MessageIds = messageIds.Select(x => x.ToString()).ToList();
        item.PublishedAt = DateTime.UtcNow;
        await context.Replace(item);
        return item;
    }

    public static List<string> Split(string content)
    {
        var sections = new List<List<string>> { new() };
        foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Trim() == MessageSeparator)
            {
                sections.Add([]);
                continue;
            }

            sections[^1].Add(line);
        }

        return sections.Select(x => string.Join('\n', x).Trim('\n', ' ')).Where(x => x.Length > 0).SelectMany(x => DiscordMessageSplitter.Split(x)).ToList();
    }

    internal static async Task<List<ulong>> Sync(IMessageChannel channel, IReadOnlyList<ulong> existingIds, IReadOnlyList<string> chunks)
    {
        var existing = new List<IUserMessage>();
        foreach (var messageId in existingIds)
        {
            if (await channel.GetMessageAsync(messageId) is IUserMessage message)
            {
                existing.Add(message);
            }
        }

        if (existing.Count == existingIds.Count && existing.Count == chunks.Count)
        {
            for (var i = 0; i < chunks.Count; i++)
            {
                if (existing[i].Content != chunks[i])
                {
                    var chunk = chunks[i];
                    await existing[i]
                        .ModifyAsync(x =>
                            {
                                x.Content = chunk;
                                x.AllowedMentions = AllowedMentions.None;
                            }
                        );
                }
            }

            return existingIds.ToList();
        }

        foreach (var message in existing)
        {
            await channel.DeleteMessageAsync(message);
        }

        var messageIds = new List<ulong>();
        foreach (var chunk in chunks)
        {
            var sent = await channel.SendMessageAsync(chunk, allowedMentions: AllowedMentions.None);
            messageIds.Add(sent.Id);
        }

        return messageIds;
    }

    private DomainDiscordChannelContent GetExisting(string id)
    {
        return context.GetSingle(id) ?? throw new NotFoundException($"Discord channel content {id} was not found");
    }

    private static void Validate(DiscordChannelContentRequest request)
    {
        if (!ulong.TryParse(request.ChannelId, out _))
        {
            throw new BadRequestException("Channel ID must be a Discord channel ID");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Name is required");
        }

        if (Split(request.Content ?? "").Count == 0)
        {
            throw new BadRequestException("Content is required");
        }
    }
}
