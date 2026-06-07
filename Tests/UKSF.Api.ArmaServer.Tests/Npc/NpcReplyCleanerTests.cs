using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcReplyCleanerTests
{
    [Theory]
    [InlineData("You said: Get back. You shouldn't be here.", "Get back. You shouldn't be here.")]
    [InlineData("you said: Leave now.", "Leave now.")]
    [InlineData("*narrows eyes* Get back.", "narrows eyes Get back.")]
    [InlineData("\"Get out of my field.\"", "Get out of my field.")]
    [InlineData("Go away. [angrily]", "Go away. angrily")]
    [InlineData("  Get out.  ", "Get out.")]
    [InlineData("Normal reply stays.", "Normal reply stays.")]
    public void Clean_StripsTranscriptFramingAndTtsUnsafeCharacters(string raw, string expected)
    {
        NpcReplyCleaner.Clean(raw).Should().Be(expected);
    }
}
