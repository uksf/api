using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Integrations.Discord.Context;
using UKSF.Api.Integrations.Discord.Models;
using UKSF.Api.Integrations.Discord.Services;
using Xunit;

namespace UKSF.Api.Integrations.Discord.Tests.Services;

public class DiscordChannelContentServiceTests
{
    private readonly Mock<IMessageChannel> _channel = new();
    private readonly Mock<IDiscordChannelContentsContext> _context = new();
    private readonly List<string> _sent = [];
    private readonly List<ulong> _deleted = [];
    private ulong _nextId = 100;

    public DiscordChannelContentServiceTests()
    {
        _channel.Setup(x => x.SendMessageAsync(
                           It.IsAny<string>(),
                           It.IsAny<bool>(),
                           It.IsAny<Embed>(),
                           It.IsAny<RequestOptions>(),
                           It.IsAny<AllowedMentions>(),
                           It.IsAny<MessageReference>(),
                           It.IsAny<MessageComponent>(),
                           It.IsAny<ISticker[]>(),
                           It.IsAny<Embed[]>(),
                           It.IsAny<MessageFlags>(),
                           It.IsAny<PollProperties>()
                       )
                )
                .ReturnsAsync((
                                  string text,
                                  bool _,
                                  Embed _,
                                  RequestOptions _,
                                  AllowedMentions mentions,
                                  MessageReference _,
                                  MessageComponent _,
                                  ISticker[] _,
                                  Embed[] _,
                                  MessageFlags _,
                                  PollProperties _
                              ) =>
                              {
                                  mentions.Should().Be(AllowedMentions.None);
                                  _sent.Add(text);
                                  var message = new Mock<IUserMessage>();
                                  message.Setup(x => x.Id).Returns(_nextId++);
                                  return message.Object;
                              }
                );
        _channel.Setup(x => x.DeleteMessageAsync(It.IsAny<IMessage>(), It.IsAny<RequestOptions>()))
                .Callback((IMessage message, RequestOptions _) => _deleted.Add(message.Id))
                .Returns(Task.CompletedTask);
    }

    [Fact]
    public void Split_uses_separator_lines_and_trims_blank_edges()
    {
        var result = DiscordChannelContentService.Split("https://x/banner.png\r\n---\r\n\r\n**Welcome**\nLine\n\n --- \n\n---\nLast");

        result.Should().Equal("https://x/banner.png", "**Welcome**\nLine", "Last");
    }

    [Fact]
    public void Split_breaks_long_sections_at_discord_limit()
    {
        var content = string.Join('\n', Enumerable.Range(0, 300).Select(i => $"Line number {i} of a long section"));

        var result = DiscordChannelContentService.Split(content);

        result.Should().HaveCountGreaterThan(1);
        result.Should().OnlyContain(x => x.Length <= DiscordMessageSplitter.MaxLength);
        string.Join('\n', result).Should().Be(content);
    }

    [Fact]
    public async Task First_publish_sends_every_chunk_without_mentions()
    {
        var result = await DiscordChannelContentService.Sync(_channel.Object, [], ["a", "b"]);

        result.Should().Equal(100UL, 101UL);
        _sent.Should().Equal("a", "b");
    }

    [Fact]
    public async Task Same_chunk_count_edits_only_changed_messages_in_place()
    {
        var first = SetupExistingMessage(1, "a");
        var second = SetupExistingMessage(2, "old");

        var result = await DiscordChannelContentService.Sync(_channel.Object, [1, 2], ["a", "new"]);

        result.Should().Equal(1UL, 2UL);
        first.Verify(x => x.ModifyAsync(It.IsAny<Action<MessageProperties>>(), It.IsAny<RequestOptions>()), Times.Never);
        var properties = new MessageProperties();
        second.Verify(x => x.ModifyAsync(It.IsAny<Action<MessageProperties>>(), It.IsAny<RequestOptions>()), Times.Once);
        second.Invocations.Single(x => x.Method.Name == "ModifyAsync").Arguments[0].As<Action<MessageProperties>>()(properties);
        properties.Content.Value.Should().Be("new");
        properties.AllowedMentions.Value.Should().Be(AllowedMentions.None);
        _sent.Should().BeEmpty();
        _deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task Different_chunk_count_replaces_all_messages()
    {
        SetupExistingMessage(1, "a");
        SetupExistingMessage(2, "b");

        var result = await DiscordChannelContentService.Sync(_channel.Object, [1, 2], ["a", "b", "c"]);

        _deleted.Should().Equal(1UL, 2UL);
        _sent.Should().Equal("a", "b", "c");
        result.Should().Equal(100UL, 101UL, 102UL);
    }

    [Fact]
    public async Task Message_deleted_in_discord_causes_full_repost()
    {
        SetupExistingMessage(1, "a");

        var result = await DiscordChannelContentService.Sync(_channel.Object, [1, 2], ["a", "b"]);

        _deleted.Should().Equal(1UL);
        _sent.Should().Equal("a", "b");
        result.Should().Equal(100UL, 101UL);
    }

    [Fact]
    public async Task Update_cannot_move_content_to_another_channel()
    {
        var item = new DomainDiscordChannelContent
        {
            ChannelId = "1",
            Name = "Lobby",
            Content = "a"
        };
        _context.Setup(x => x.GetSingle(item.Id)).Returns(item);
        var service = new DiscordChannelContentService(_context.Object, new Mock<IDiscordClientService>().Object);

        var act = () => service.Update(item.Id, new DiscordChannelContentRequest("2", "Lobby", "b"));

        await act.Should().ThrowAsync<BadRequestException>();
        _context.Verify(x => x.Replace(It.IsAny<DomainDiscordChannelContent>()), Times.Never);
    }

    [Theory]
    [InlineData("abc", "Lobby", "a")]
    [InlineData("1", " ", "a")]
    [InlineData("1", "Lobby", "\n---\n")]
    public async Task Create_rejects_invalid_requests(string channelId, string name, string content)
    {
        var service = new DiscordChannelContentService(_context.Object, new Mock<IDiscordClientService>().Object);

        var act = () => service.Create(new DiscordChannelContentRequest(channelId, name, content));

        await act.Should().ThrowAsync<BadRequestException>();
        _context.Verify(x => x.Add(It.IsAny<DomainDiscordChannelContent>()), Times.Never);
    }

    private Mock<IUserMessage> SetupExistingMessage(ulong id, string content)
    {
        var message = new Mock<IUserMessage>();
        message.Setup(x => x.Id).Returns(id);
        message.Setup(x => x.Content).Returns(content);
        message.Setup(x => x.ModifyAsync(It.IsAny<Action<MessageProperties>>(), It.IsAny<RequestOptions>())).Returns(Task.CompletedTask);
        _channel.Setup(x => x.GetMessageAsync(id, It.IsAny<CacheMode>(), It.IsAny<RequestOptions>())).ReturnsAsync(message.Object);
        return message;
    }
}
