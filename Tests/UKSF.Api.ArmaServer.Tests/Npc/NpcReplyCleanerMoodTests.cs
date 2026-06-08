using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcReplyCleanerMoodTests
{
    [Fact]
    public void Extracts_a_valid_leading_mood_tag_and_strips_it()
    {
        var (mood, rest) = NpcReplyCleaner.ExtractMood("[mood:angry] Get back, now.");
        mood.Should().Be("angry");
        rest.Should().Be("Get back, now.");
    }

    [Fact]
    public void Tolerates_whitespace_and_casing()
    {
        var (mood, rest) = NpcReplyCleaner.ExtractMood("  [MOOD: Afraid ]  They're everywhere!");
        mood.Should().Be("afraid");
        rest.Should().Be("They're everywhere!");
    }

    [Fact]
    public void Absent_tag_defaults_to_neutral_and_keeps_text()
    {
        var (mood, rest) = NpcReplyCleaner.ExtractMood("Hold the line.");
        mood.Should().Be("neutral");
        rest.Should().Be("Hold the line.");
    }

    [Fact]
    public void Unknown_mood_defaults_to_neutral_and_strips_the_tag()
    {
        var (mood, rest) = NpcReplyCleaner.ExtractMood("[mood:furious] Move!");
        mood.Should().Be("neutral");
        rest.Should().Be("Move!");
    }

    [Fact]
    public void A_tag_not_at_the_start_is_left_untouched()
    {
        var (mood, rest) = NpcReplyCleaner.ExtractMood("Go [mood:sad] now");
        mood.Should().Be("neutral");
        rest.Should().Be("Go [mood:sad] now");
    }
}
