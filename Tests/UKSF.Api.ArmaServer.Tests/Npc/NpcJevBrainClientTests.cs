using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcJevBrainClientTests
{
    private readonly Mock<INpcJevClient> _jev = new();
    private readonly Mock<IClacksClient> _clacks = new();
    private readonly NpcJevBrainClient _sut;

    public NpcJevBrainClientTests()
    {
        var voices = new Mock<INpcVoicesContext>();
        var logger = Mock.Of<IUksfLogger>();
        var oneCall = new NpcBrainService(_clacks.Object, voices.Object, logger);
        _sut = new NpcJevBrainClient(oneCall, new NpcGuardedJevBrain(_jev.Object, _clacks.Object, logger), new NpcPlainJevBrain(_jev.Object, _clacks.Object, logger), _jev.Object, logger);
    }

    private void JevAnswers(Dictionary<string, JevAnswer> answers) =>
        _jev.Setup(x => x.AskAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, JevQuestion>>(), It.IsAny<string>()))
            .ReturnsAsync((string _, Dictionary<string, JevQuestion> q, string _) => new JevResult
                {
                    Answers = q.Keys.ToDictionary(k => k, k => answers.TryGetValue(k, out var a) ? a : new JevAnswer { Type = "noul", Noul = 0.1 }),
                    Ms = 250
                }
            );

    private void WriterSays(string text, string kind = null) =>
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
               .ReturnsAsync(new ClacksChatResult { Text = $"{{\"text\":\"{text}\",\"emote\":null}}", Model = "gemini", Node = "ultron", Ms = 600 });

    private static JevAnswer Pick(string choice) => new() { Type = "choice", Choice = choice, Confidence = 0.9 };
    private static JevAnswer Yes => new() { Type = "noul", Noul = 0.9 };

    private static RespondRequest Plain(string mode = "dynamic") =>
        new()
        {
            NpcId = "pavel",
            Persona = new NpcPersona { Name = "Pavel", Role = "trader", Language = "English", Mood = "chatty", AttitudeToPlayers = "friendly" },
            Knowledge = "Sells bread.",
            Mode = mode,
            Scripted = mode == "scripted"
                ? new NpcScriptedDto { Lines = [new NpcScriptedLine { Id = "bread", Topic = "bread", Line = "Two coins a loaf." }], Deflection = "Bread?" }
                : null,
            NewTurns = [new NpcTurnDto { SpeakerName = "Player", Text = "How much for the bread?", T = 1 }],
            TextOnly = mode != "scripted"
        };

    [Fact]
    public async Task Plain_UsesJevMoodAndWriterText()
    {
        JevAnswers(new() { ["mood"] = Pick("happy"), ["known"] = Yes });
        WriterSays("Two coins, friend.");

        var result = await _sut.RespondAsync(Plain());

        result.Text.Should().Be("Two coins, friend.");
        result.Mood.Should().Be("happy");
        result.Provider.Should().Be("jev+writer");
    }

    [Fact]
    public async Task Plain_JevDown_FallsBackToOneCall()
    {
        _jev.Setup(x => x.AskAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, JevQuestion>>(), It.IsAny<string>())).ReturnsAsync((JevResult)null);
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
               .ReturnsAsync(new ClacksChatResult { Text = "[mood:neutral] Two coins.", Model = "gemini", Node = "ultron" });
        WriterSays("unused");

        var result = await _sut.RespondAsync(Plain());

        result.Text.Should().Be("Two coins.");
        result.Provider.Should().Be("gemini@ultron");
    }

    [Fact]
    public async Task Scripted_PicksTheJevLineWithoutAWriter()
    {
        JevAnswers(new() { ["mood"] = Pick("neutral"), ["line"] = Pick("bread") });

        var result = await _sut.RespondAsync(Plain("scripted"));

        result.LineId.Should().Be("bread");
        result.Text.Should().Be("Two coins a loaf.");
        _clacks.Verify(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Scripted_InjectionDeflects()
    {
        JevAnswers(new() { ["mood"] = Pick("neutral"), ["line"] = Pick("bread"), ["injection"] = Yes });

        var result = await _sut.RespondAsync(Plain("scripted"));

        result.LineId.Should().Be(NpcPromptBuilder.Deflection);
        result.Text.Should().Be("Bread?");
    }

    private static NpcGuardedTurnRequest Guarded(List<string> disclosed = null)
    {
        var config = new NpcGuardedConfig
        {
            Concern = "family",
            Facts =
            [
                new NpcGuardedFact { Id = "f1", Topic = "traffic", Text = "Trucks pass at night." },
                new NpcGuardedFact { Id = "f2", Topic = "mill", Text = "They stop at the mill." },
                new NpcGuardedFact { Id = "f3", Topic = "return", Text = "Every third night." }
            ]
        };
        return new NpcGuardedTurnRequest
        {
            NpcId = "tomas",
            Persona = new NpcPersona { Name = "Tomas", Role = "farmer", Language = "English", Mood = "wary", AttitudeToPlayers = "cautious" },
            Config = config,
            State = new NpcGuardedState { DisclosedFactIds = disclosed ?? [] },
            NewTurns = [new NpcTurnDto { SpeakerName = "Player", Text = "Seen trucks?", T = 1 }]
        };
    }

    [Fact]
    public async Task Guarded_UnlockedFact_IsClaimedByTheEngine()
    {
        JevAnswers(new() { ["u0_tag"] = Pick(NpcGuardedTags.RelevantQuestion), ["u0_topic"] = Pick("1"), ["mood"] = Pick("afraid") });
        WriterSays("Trucks, most nights.");

        var turn = await _sut.TurnGuardedAsync(Guarded());

        turn.Reply.Ok.Should().BeTrue();
        turn.Reply.DisclosedFactId.Should().Be("f1");
        turn.Classify.Classifications.Single().TopicSlot.Should().Be(1);
    }

    [Fact]
    public async Task Guarded_QuestionOnALaterTopic_ClaimsNothingAndTheWriterNeverSeesIt()
    {
        JevAnswers(new() { ["u0_tag"] = Pick(NpcGuardedTags.RelevantQuestion), ["u0_topic"] = Pick("2"), ["mood"] = Pick("neutral") });
        string writerSystem = null;
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
               .Callback<string, string, string, bool, int, double, object>((_, system, _, _, _, _, _) => writerSystem = system)
               .ReturnsAsync(new ClacksChatResult { Text = "{\"text\":\"Can't say.\"}", Ms = 500 });

        var turn = await _sut.TurnGuardedAsync(Guarded());

        turn.Reply.DisclosedFactId.Should().BeNull();
        writerSystem.Should().NotContain("mill").And.NotContain("Trucks pass").And.NotContain("third night");
    }

    [Fact]
    public async Task IsAddressed_ReadsJev_AndIsNullWhenJevIsDown()
    {
        JevAnswers(new() { ["to_me"] = Yes });
        (await _sut.IsAddressedAsync(["Pavel", "Tomas"], "Pavel", false, "Parval, what did you see?", "pavel")).Should().BeTrue();

        _jev.Setup(x => x.AskAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, JevQuestion>>(), It.IsAny<string>())).ReturnsAsync((JevResult)null);
        (await _sut.IsAddressedAsync(["Pavel", "Tomas"], "Pavel", false, "Parval, what did you see?", "pavel")).Should().BeNull();
    }

    [Fact]
    public async Task Guarded_JevDown_FailsClosed()
    {
        _jev.Setup(x => x.AskAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, JevQuestion>>(), It.IsAny<string>())).ReturnsAsync((JevResult)null);

        var turn = await _sut.TurnGuardedAsync(Guarded());

        turn.Classify.Should().BeNull();
        turn.Reply.Ok.Should().BeFalse();
        _clacks.VerifyNoOtherCalls();
    }
}
