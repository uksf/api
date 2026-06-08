using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class MoodScriptsTests
{
    [Fact]
    public void All_moods_are_neutral_plus_the_four_generated()
    {
        MoodScripts.All.Should().BeEquivalentTo(["neutral", "angry", "afraid", "sad", "happy"]);
        MoodScripts.Generated.Should().BeEquivalentTo(["angry", "afraid", "sad", "happy"]);
        MoodScripts.Generated.Should().NotContain(MoodScripts.Neutral);
    }

    [Fact]
    public void Every_generated_mood_has_a_descriptor_and_a_script()
    {
        foreach (var mood in MoodScripts.Generated)
        {
            MoodScripts.Table.Should().ContainKey(mood);
            MoodScripts.Table[mood].EmoText.Should().NotBeNullOrWhiteSpace();
            MoodScripts.Table[mood].Script.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData("neutral", true)]
    [InlineData("angry", true)]
    [InlineData("happy", true)]
    [InlineData("furious", false)]
    [InlineData("", false)]
    public void IsValid_accepts_only_known_moods(string mood, bool expected)
    {
        MoodScripts.IsValid(mood).Should().Be(expected);
    }
}
