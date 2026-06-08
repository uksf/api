using System;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class VoiceSlugTests
{
    [Theory]
    [InlineData("Smuggler", null, null, "smuggler")]
    [InlineData("Old Sailor Pete", null, null, "old_sailor_pete")]
    [InlineData("Café Owner!!", null, null, "caf_owner")]
    [InlineData("Smuggler", "smuggler", "Angry", "smuggler_angry")]
    [InlineData("ignored", "smuggler", "Very Angry", "smuggler_very_angry")]
    public void Derives_expected_slug(string displayName, string moodOf, string moodLabel, string expected)
    {
        VoiceSlug.Derive(displayName, moodOf, moodLabel).Should().Be(expected);
    }

    [Fact]
    public void Empty_input_throws()
    {
        var act = () => VoiceSlug.Derive("!!!", null, null);
        act.Should().Throw<ArgumentException>();
    }
}
