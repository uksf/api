using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class MoodScriptsTests
{
    [Fact]
    public void Every_mood_including_neutral_is_generated()
    {
        MoodScripts.All.Should().BeEquivalentTo(["neutral", "angry", "afraid", "sad", "happy"]);

        // neutral is rendered by the same engine as the moods, from the same seed, so a
        // player never hears one engine for neutral and another the moment a mood turns.
        MoodScripts.Generated.Should().Contain(MoodScripts.Neutral);
    }

    [Fact]
    public void Every_generated_mood_has_a_descriptor_and_a_script()
    {
        foreach (var mood in MoodScripts.Generated)
        {
            MoodScripts.Table.Should().ContainKey(mood);
            MoodScripts.Table[mood].EmoText.Should().NotBeNullOrWhiteSpace();
            MoodScripts.Table[mood].Script.Should().Be(MoodScripts.Script);
        }
    }

    [Fact]
    public void Every_mood_reads_the_same_script_so_only_delivery_differs()
    {
        MoodScripts.Table.Values.Select(x => x.Script).Distinct().Should().HaveCount(1);
        MoodScripts.Table.Values.Select(x => x.EmoText).Distinct().Should().HaveCount(MoodScripts.Generated.Count);
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

    [Theory]
    [InlineData("afraid", "afraid")]
    [InlineData(" ANGRY ", "angry")]
    [InlineData("wary", "neutral")]
    [InlineData("", "neutral")]
    [InlineData(null, "neutral")]
    public void Normalise_keeps_known_moods_and_falls_back_to_neutral(string mood, string expected)
    {
        MoodScripts.Normalise(mood).Should().Be(expected);
    }
}
