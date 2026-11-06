using System.Linq;
using FluentAssertions;
using UKSF.Api.Integrations.Discord.Services;
using Xunit;

namespace UKSF.Api.Integrations.Discord.Tests.Services;

public class DiscordMessageSplitterTests
{
    [Fact]
    public void Short_message_is_sent_whole()
    {
        DiscordMessageSplitter.Split("a\nb").Should().Equal("a\nb");
    }

    [Fact]
    public void Long_message_splits_on_line_boundaries()
    {
        var result = DiscordMessageSplitter.Split("> aaa\n> bbb\n> ccc", 11);

        result.Should().Equal("> aaa\n> bbb", "> ccc");
    }

    [Fact]
    public void Line_longer_than_limit_is_hard_split()
    {
        var result = DiscordMessageSplitter.Split("ab\ncdefgh", 4);

        result.Should().Equal("ab", "cdef", "gh");
    }

    [Fact]
    public void Real_changelog_fits_discord_limit_and_keeps_all_text()
    {
        var lines = Enumerable.Range(0, 120).Select(i => $"> - Subsystem {i} - Some gameplay-facing change description").ToList();
        var message = "@here\nModpack Update - 5.23.17\n\n" + string.Join("\n", lines);

        var result = DiscordMessageSplitter.Split(message);

        result.Should().HaveCountGreaterThan(1);
        result.Should().OnlyContain(chunk => chunk.Length <= 2000);
        string.Join("\n", result).Should().Be(message);
    }
}
