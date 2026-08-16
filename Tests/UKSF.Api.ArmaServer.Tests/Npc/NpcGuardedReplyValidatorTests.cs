using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcGuardedReplyValidatorTests
{
    private static readonly NpcGuardedConfig Config = new()
    {
        Concern = "family",
        Facts =
        [
            new NpcGuardedFact
            {
                Id = "f1",
                Topic = "traffic",
                Text = "Trucks roll past after dark."
            },
            new NpcGuardedFact
            {
                Id = "f2",
                Topic = "stop",
                Text = "They stop at the old mill."
            },
            new NpcGuardedFact
            {
                Id = "f3",
                Topic = "return",
                Text = "They return near midnight."
            }
        ]
    };

    [Fact]
    public void ValidReply_WithMatchingId_ComposesCanonicalOnce()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput
            {
                Text = "I saw something.",
                Mood = "afraid",
                Emote = "looks down",
                DisclosedFactId = "f1"
            },
            Config,
            "f1",
            Config.Facts[0].Text
        );

        result.Ok.Should().BeTrue();
        result.DisclosedFactId.Should().Be("f1");
        result.SpokenText.Should().Be("I saw something. Trucks roll past after dark.");
        result.Mood.Should().Be("afraid");
        result.Emote.Should().Be("looks down");
    }

    [Fact]
    public void CanonicalTextWithoutId_Rejected()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput { Text = "Trucks roll past after dark.", Mood = "neutral" },
            Config,
            "f1",
            Config.Facts[0].Text
        );
        result.Ok.Should().BeFalse();
        result.Failure.Should().Contain("canonical");
    }

    [Fact]
    public void UnauthorisedFactId_Rejected()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput
            {
                Text = "Maybe later.",
                Mood = "neutral",
                DisclosedFactId = "f2"
            },
            Config,
            "f1",
            Config.Facts[0].Text
        );
        result.Ok.Should().BeFalse();
        result.Failure.Should().Contain("unauthorised");
    }

    [Fact]
    public void EmptyText_Rejected()
    {
        NpcGuardedReplyValidator.Validate(new NpcGuardedReplyModelOutput { Text = "  ", Mood = "neutral" }, Config, null, null).Ok.Should().BeFalse();
    }

    [Fact]
    public void InvalidMood_FallsBackToNeutral_AndKeepsText()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput { Text = "Only what I see from my fields.", Mood = "wary" },
            Config,
            null,
            null
        );
        result.Ok.Should().BeTrue();
        result.Mood.Should().Be(MoodScripts.Neutral);
        result.SpokenText.Should().Be("Only what I see from my fields.");
    }

    [Fact]
    public void OverlongEmote_Rejected()
    {
        NpcGuardedReplyValidator.Validate(
                                    new NpcGuardedReplyModelOutput
                                    {
                                        Text = "Hi",
                                        Mood = "neutral",
                                        Emote = new string('x', 50)
                                    },
                                    Config,
                                    null,
                                    null
                                )
                                .Ok.Should()
                                .BeFalse();
    }

    [Fact]
    public void CanonicalFactTextInEmote_Rejected()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput
            {
                Text = "Hi",
                Mood = "neutral",
                Emote = "They stop at the old mill."
            },
            Config,
            null,
            null
        );

        result.Ok.Should().BeFalse();
        result.Failure.Should().Contain("emote");
    }

    [Fact]
    public void ConcernTextInEmote_Rejected()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput
            {
                Text = "Hi",
                Mood = "neutral",
                Emote = "worries about family"
            },
            Config,
            null,
            null
        );

        result.Ok.Should().BeFalse();
        result.Failure.Should().Contain("concern");
    }

    [Fact]
    public void MatchingIdWithoutPermitText_Rejected()
    {
        NpcGuardedReplyValidator.Validate(
                                    new NpcGuardedReplyModelOutput
                                    {
                                        Text = "Hi",
                                        Mood = "neutral",
                                        DisclosedFactId = "f1"
                                    },
                                    Config,
                                    "f1",
                                    null
                                )
                                .Ok.Should()
                                .BeFalse();
    }

    [Fact]
    public void ValidWithoutDisclosure_Passes()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput { Text = "I keep to myself.", Mood = "neutral" },
            Config,
            "f1",
            Config.Facts[0].Text
        );
        result.Ok.Should().BeTrue();
        result.DisclosedFactId.Should().BeNull();
        result.SpokenText.Should().Be("I keep to myself.");
    }

    [Fact]
    public void DisclosedCanonicalText_AllowedWhenInLedger()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput { Text = "As I said — Trucks roll past after dark.", Mood = "neutral" },
            Config,
            null,
            null,
            ["f1"]
        );
        result.Ok.Should().BeTrue();
        result.SpokenText.Should().Contain("Trucks roll past after dark.");
    }

    [Fact]
    public void UndisclosedCanonicalText_StillBlocked()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput { Text = "They stop at the old mill.", Mood = "neutral" },
            Config,
            null,
            null,
            ["f1"]
        );
        result.Ok.Should().BeFalse();
        result.Failure.Should().Contain("canonical");
    }
}
