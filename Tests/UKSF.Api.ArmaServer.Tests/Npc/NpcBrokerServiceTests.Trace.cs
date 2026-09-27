using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceTests
{
    private static Dictionary<string, object> MakeTracedTurnData() =>
        MakeTurnData(
            newTurns:
            [
                new Dictionary<string, object>
                {
                    ["speakerId"] = "76561",
                    ["text"] = "where is the ammo?",
                    ["t"] = 1700000000000L,
                    ["utt"] = "76561:3"
                }
            ]
        );

    private void SetupSpokenReply(string emote)
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>()))
        .ReturnsAsync(
            new RespondResult
            {
                Text = "go away",
                Mood = "angry",
                Emote = emote,
                VoiceId = "bm_george",
                Provider = "jev+writer",
                Decision = new NpcPlainDecision { Mood = "angry", Known = true }
            }
        );
        _clacks.Setup(x => x.SpeakStreamAsync("npc-voice", "go away", "bm_george", It.IsAny<Func<string, Task>>()))
               .Returns(async (string r, string t, string v, Func<string, Task> onFrame) =>
                   {
                       await onFrame("QQ==");
                       await onFrame("QQ==");
                   }
               );
    }

    [Fact]
    public async Task HandleTurnAsync_PlainTurnWithEmote_SendsEmoteAfterSpeech()
    {
        SetupSpokenReply("shakes his head");
        var commands = new List<string>();
        _commandSender.Setup(x => x.SendCommandAsync(5006, It.IsAny<string>())).Callback<int, string>((_, c) => commands.Add(c)).Returns(Task.CompletedTask);

        await _sut.HandleTurnAsync(5006, MakeTracedTurnData());

        var emote = commands.FindIndex(c => c.StartsWith("[\"npc_emote\""));
        emote.Should().BeGreaterThan(commands.FindIndex(c => c.Contains("npc_audio_end")));
        commands[emote].Should().Be("[\"npc_emote\",\"npc1\",\"turn7\",\"shakes his head\"]");
    }

    [Fact]
    public async Task HandleTurnAsync_PlainTurnWithoutEmote_SendsNoEmote()
    {
        SetupSpokenReply(null);

        await _sut.HandleTurnAsync(5006, MakeTracedTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_emote"))), Times.Never);
        _trace.Single("turn.finished").Data.Contains("emoteSent").Should().BeFalse();
    }

    [Fact]
    public async Task HandleTurnAsync_SpokenTurn_TracesDecidedRepliedFinished()
    {
        SetupSpokenReply("shrugs");

        await _sut.HandleTurnAsync(5006, MakeTracedTurnData());

        _trace.Events.Select(e => e.Type).Should().Equal("turn.decided", "turn.replied", "turn.finished");
        _trace.Events.Should().OnlyContain(e => e.Session == "session1" && e.Npc == "npc1" && e.Turn == "turn7");

        var decided = _trace.Single("turn.decided").Data;
        decided["address"].AsString.Should().Be("answer");
        decided["gaze"].AsBoolean.Should().BeTrue();
        decided["utts"][0].AsString.Should().Be("76561:3");
        decided["decision"]["mood"].AsString.Should().Be("angry");

        var replied = _trace.Single("turn.replied").Data;
        replied["reply"]["text"].AsString.Should().Be("go away");
        replied["reply"]["emote"].AsString.Should().Be("shrugs");

        var finished = _trace.Single("turn.finished").Data;
        finished["outcome"].AsString.Should().Be("spoke");
        finished["tts"]["frames"].ToInt32().Should().Be(2);
        finished["tts"]["delivered"].AsBoolean.Should().BeTrue();
        finished["emoteSent"].AsBoolean.Should().BeTrue();
        finished["committed"].AsBoolean.Should().BeTrue();
    }

    [Fact]
    public async Task HandleTurnAsync_BrainFails_StillFinishesTrace()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>())).ReturnsAsync((RespondResult)null);

        await _sut.HandleTurnAsync(5006, MakeTracedTurnData());

        _trace.Single("turn.finished").Data["outcome"].AsString.Should().Be("brain failed");
        _trace.Single("turn.replied").Data.Contains("reply").Should().BeFalse();
    }

    [Fact]
    public async Task HandleTurnAsync_Throws_StillFinishesTrace()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>())).ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.HandleTurnAsync(5006, MakeTracedTurnData());

        await act.Should().ThrowAsync<InvalidOperationException>();
        _trace.Single("turn.finished").Data["outcome"].AsString.Should().Be("unknown");
    }

    [Fact]
    public async Task Register_ThenMissionEnd_TracesMissionLifecycleOnce()
    {
        await _sut.HandleRegisterAsync(5006, MakeRegisterData());
        await _sut.HandleRegisterAsync(5006, MakeRegisterData(npcId: "npc2"));
        _sessionsContext.Setup(x => x.Get(It.IsAny<Func<DomainNpcSession, bool>>())).Returns([MakeDynamicSession()]);
        await _sut.HandleMissionEndedAsync("session1");

        _trace.Events.Select(e => e.Type).Should().Equal("mission.started", "npc.registered", "npc.registered", "mission.ended");
        var registered = _trace.Events.First(e => e.Type == "npc.registered");
        registered.Npc.Should().Be("npc1");
        registered.Data["persona"]["name"].AsString.Should().Be("Asad");
        registered.Data["knowledge"].AsString.Should().Be("knows the location");
        var ended = _trace.Single("mission.ended").Data;
        ended["reason"].AsString.Should().Be("clean");
        ended["npcs"][0]["npc"].AsString.Should().Be("npc1");
    }

    [Fact]
    public async Task MissionEnd_WithoutNpcs_WritesNothing()
    {
        _sessionsContext.Setup(x => x.Get(It.IsAny<Func<DomainNpcSession, bool>>())).Returns([]);

        await _sut.HandleMissionEndedAsync("session1");

        _trace.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleTraceEvent_Utterance_RecordsArmaPayload()
    {
        await _sut.HandleTraceEventAsync(
            "npc_utterance",
            new Dictionary<string, object>
            {
                ["sessionId"] = "session1",
                ["utt"] = "76561:3",
                ["uid"] = "76561",
                ["name"] = "Beswick",
                ["text"] = "hello?",
                ["heard"] = new List<object> { new List<object> { "npc1", "accepted" } },
                ["reason"] = ""
            }
        );

        var utterance = _trace.Single("utterance.received");
        utterance.Utt.Should().Be("76561:3");
        utterance.Data["text"].AsString.Should().Be("hello?");
        utterance.Data["heard"][0][1].AsString.Should().Be("accepted");
        utterance.Data.Contains("sessionId").Should().BeFalse();
        _trace.Events.First().Type.Should().Be("mission.started");
    }

    [Fact]
    public async Task HandleTraceEvent_Ack_RecordsTurn()
    {
        await _sut.HandleTraceEventAsync(
            "npc_ack",
            new Dictionary<string, object>
            {
                ["sessionId"] = "session1",
                ["npc"] = "npc1",
                ["turn"] = "turn7",
                ["kind"] = "stream",
                ["ok"] = false,
                ["reason"] = "terminal"
            }
        );

        var ack = _trace.Single("turn.acked");
        ack.Npc.Should().Be("npc1");
        ack.Turn.Should().Be("turn7");
        ack.Data["reason"].AsString.Should().Be("terminal");
        ack.Data["ok"].AsBoolean.Should().BeFalse();
    }

    [Fact]
    public async Task HandleTurnAsync_EmoteSendStalls_TurnStillFinishes()
    {
        SetupSpokenReply("shrugs");
        _commandSender.Setup(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_emote")))).Returns(new TaskCompletionSource().Task);

        var turn = _sut.HandleTurnAsync(5006, MakeTracedTurnData());

        (await Task.WhenAny(turn, Task.Delay(2000))).Should().BeSameAs(turn);
        _trace.Single("turn.finished").Data["outcome"].AsString.Should().Be("spoke");
    }

    [Fact]
    public async Task HandleTurnAsync_NoNewTurns_FinishesTraceAsEmpty()
    {
        await _sut.HandleTurnAsync(5006, MakeTurnData(newTurns: []));

        _trace.Single("turn.finished").Data["outcome"].AsString.Should().Be("empty");
    }
}
