using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcTextSanitiserTests
{
    [Fact]
    public void StripsControlCharsAndNewlines() => NpcTextSanitiser.Sanitise("hello\nthere\tfriend").Should().Be("hello there friend");

    [Fact]
    public void CollapsesWhitespace() => NpcTextSanitiser.Sanitise("too    many     spaces").Should().Be("too many spaces");

    [Fact]
    public void Trims() => NpcTextSanitiser.Sanitise("   padded   ").Should().Be("padded");

    [Fact]
    public void CapsLength() => NpcTextSanitiser.Sanitise(new string('a', 1000)).Length.Should().Be(500);

    [Fact]
    public void NullOrEmptyBecomesEmpty()
    {
        NpcTextSanitiser.Sanitise(null).Should().BeEmpty();
        NpcTextSanitiser.Sanitise("   ").Should().BeEmpty();
    }

    [Fact]
    public void KeepsNormalPunctuation() => NpcTextSanitiser.Sanitise("Where's the cache, friend?").Should().Be("Where's the cache, friend?");

    [Theory]
    [InlineData("[BLANK_AUDIO]")]
    [InlineData(" [blank_audio] ")]
    [InlineData("[SILENCE]")]
    [InlineData("(blank)")]
    public void DropsWhisperNonSpeechTokens(string input) => NpcTextSanitiser.Sanitise(input).Should().BeEmpty();

    [Fact]
    public void StripsJunkTokenButKeepsRealWords() =>
        NpcTextSanitiser.Sanitise("Yes. [BLANK_AUDIO]").Should().Be("Yes.");

    [Fact]
    public void KeepsShortAck() => NpcTextSanitiser.Sanitise("Yeah.").Should().Be("Yeah.");
}
