using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcGuardedReplyValidatorTests
{
    private static readonly NpcGuardedFact Next = new()
    {
        Id = "1",
        Topic = "traffic",
        Text = "Trucks roll past after dark."
    };

    [Fact]
    public void MatchingId_KeepsModelText()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput
            {
                Text = "Grey pickup, three men, none of them local.",
                Mood = "afraid",
                Emote = "looks down",
                DisclosedFactId = "1"
            },
            Next
        );

        result.Ok.Should().BeTrue();
        result.SpokenText.Should().Be("Grey pickup, three men, none of them local.");
        result.DisclosedFactId.Should().Be("1");
        result.Mood.Should().Be("afraid");
    }

    [Theory]
    [InlineData("2", "g2")]
    [InlineData("g2", "2")]
    [InlineData("2", "f2")]
    public void BareSlotNumber_MatchesPrefixedId(string claimed, string includedId)
    {
        var next = new NpcGuardedFact { Id = includedId, Text = "They stop at the old mill." };
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput
            {
                Text = "It sat at the grain store a long while.",
                Mood = "neutral",
                DisclosedFactId = claimed
            },
            next
        );

        result.Ok.Should().BeTrue();
        result.SpokenText.Should().Be("It sat at the grain store a long while.");
        result.DisclosedFactId.Should().Be(includedId);
    }

    [Fact]
    public void CanonicalSentenceInText_IsNotAFail()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput { Text = "Trucks roll past after dark.", Mood = "neutral" },
            Next
        );
        result.Ok.Should().BeTrue();
        result.SpokenText.Should().Be("Trucks roll past after dark.");
        result.DisclosedFactId.Should().Be("1");
    }

    [Fact]
    public void EmptyText_Rejected()
    {
        NpcGuardedReplyValidator.Validate(new NpcGuardedReplyModelOutput { Text = "  ", Mood = "neutral" }, Next).Ok.Should().BeFalse();
    }

    [Fact]
    public void InvalidMood_FallsBackToNeutral_AndKeepsText()
    {
        var result = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput { Text = "I keep to myself.", Mood = "wary" },
            includedNext: null
        );
        result.Ok.Should().BeTrue();
        result.Mood.Should().Be(MoodScripts.Neutral);
        result.SpokenText.Should().Be("I keep to myself.");
        result.DisclosedFactId.Should().BeNull();
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
                                 Next
                             )
                             .Ok.Should()
                             .BeFalse();
    }

    private static readonly NpcGuardedConfig Config = new()
    {
        Facts =
        [
            new NpcGuardedFact { Id = "1", Topic = "traffic", Text = "Trucks roll past after dark." },
            new NpcGuardedFact { Id = "2", Topic = "mill", Text = "The mill hides fuel drums." },
            new NpcGuardedFact { Id = "3", Topic = "names", Text = "Captain Rusk runs the convoys." }
        ]
    };

    private static NpcGuardedValidatedReply Reply(string text, string told = null, string emote = null) =>
        new() { Ok = true, SpokenText = text, Emote = emote, DisclosedFactId = told, Mood = "neutral" };

    [Fact]
    public void Guard_KeepsTheNextFactWhenTheEnginePermitsIt()
    {
        NpcGuardedReplyValidator.Guard(Reply("Trucks, most nights.", "1"), "1", "1", Config, [], "f1").Ok.Should().BeTrue();
    }

    [Fact]
    public void Guard_RejectsTheNextFactOnATurnTheEngineDidNotUnlock()
    {
        NpcGuardedReplyValidator.Guard(Reply("Trucks, most nights.", "1"), "1", "1", Config, [], null).Failure.Should().Be("disclosure not permitted");
        // Validate already dropped the claim; the raw claim still counts.
        NpcGuardedReplyValidator.Guard(Reply("Trucks, most nights."), "f1", "1", Config, [], null).Failure.Should().Be("disclosure not permitted");
    }

    [Fact]
    public void Guard_IgnoresAStrayClaimOfAFactTheModelNeverSaw()
    {
        NpcGuardedReplyValidator.Guard(Reply("It didn't stop near my patch."), "2", "1", Config, [], null).Ok.Should().BeTrue();
    }

    [Fact]
    public void Guard_RejectsWithheldFactTextInSpeechOrEmote()
    {
        NpcGuardedReplyValidator.Guard(Reply("the mill hides fuel drums, you know"), null, "1", Config, [], "1").Failure.Should().Be("withheld fact text in reply");
        NpcGuardedReplyValidator.Guard(Reply("Nothing.", emote: "Captain Rusk runs the convoys."), null, "1", Config, [], null).Ok.Should().BeFalse();
    }

    [Fact]
    public void Guard_AllowsFactsAlreadyToldOrPermitted()
    {
        NpcGuardedReplyValidator.Guard(Reply("Like I said, trucks roll past after dark."), null, "2", Config, ["1"], null).Ok.Should().BeTrue();
        NpcGuardedReplyValidator.Guard(Reply("Trucks roll past after dark.", "1"), "1", "1", Config, [], "1").Ok.Should().BeTrue();
    }
}
