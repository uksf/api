using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

/// Extra broker path coverage kept separate to stay under the 300-line file cap.
public partial class NpcBrokerServiceGuardedTests
{
    [Fact]
    public async Task FirstThreat_StreamsWarnFallback_AndCommitsWarning()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.Threat));
        // Model claims unauthorised id → validation fails → warn fallback + neutral voice, still commits warn.
        SetupReply("whatever", "afraid", null, "f9");

        string spoken = null;
        string voice = null;
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Callback<string, string, string, Func<string, Task>>((_, text, v, _) =>
                   {
                       spoken = text;
                       voice = v;
                   }
               )
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, TurnData());

        spoken.Should().Be(NpcGuardedProfile.WarnFallback);
        voice.Should().Be("bm_george");
        _updates.Should().BeGreaterThan(0);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_guarded_state"))), Times.Once);
    }

    [Fact]
    public async Task BackOff_WhileWarning_CommitsClearedWarning()
    {
        _session = MakeGuardedSession();
        _session.GuardedState.PendingWarning = true;
        _session.GuardedState.CooperationBand = NpcCooperationBands.Closed;
        SetupClassify(Tag(NpcGuardedTags.BackOff));
        _brain.Setup(x => x.ReplyGuardedAsync(It.IsAny<NpcGuardedReplyRequest>()))
              .ReturnsAsync(new NpcGuardedReplyResult { Ok = false, Failure = "null model" });

        string spoken = null;
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Callback<string, string, string, Func<string, Task>>((_, text, _, _) => spoken = text)
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, TurnData());

        spoken.Should().Be(NpcGuardedProfile.BackOffFallback);
        _updates.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RepeatThreat_BurnsAndCommits()
    {
        _session = MakeGuardedSession();
        _session.GuardedState.PendingWarning = true;
        SetupClassify(Tag(NpcGuardedTags.Threat));
        _brain.Setup(x => x.ReplyGuardedAsync(It.IsAny<NpcGuardedReplyRequest>())).ReturnsAsync(new NpcGuardedReplyResult { Ok = false, Failure = "boom" });

        string spoken = null;
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Callback<string, string, string, Func<string, Task>>((_, text, _, _) => spoken = text)
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, TurnData());

        spoken.Should().Be(NpcGuardedProfile.BurnedFallback);
        _updates.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UnauthorisedFactId_SafeDeflection_NoDisclosureCommit()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("secrets", "neutral", null, "f2");

        string spoken = null;
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Callback<string, string, string, Func<string, Task>>((_, text, _, _) => spoken = text)
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, TurnData());

        spoken.Should().Be(NpcGuardedProfile.SafeDeflection);
        // normal/disclose validation failure does not commit state
        _updates.Should().Be(0);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_guarded_state"))), Times.Never);
    }

    [Fact]
    public async Task TtsThrow_LeavesStateUnchanged()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("Listen carefully.", "neutral", null, "f1");
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .ThrowsAsync(new InvalidOperationException("tts down"));

        await _sut.HandleTurnAsync(5006, TurnData());

        _updates.Should().Be(0);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_guarded_state"))), Times.Never);
    }

    [Fact]
    public async Task ConversationBorderline_StillUsesRespondPath()
    {
        _session = MakeGuardedSession();
        _session.InteractionProfile = NpcInteractionProfiles.Conversation;
        _session.Persona.Name = "Merl";
        _session.Guarded = null;
        _session.GuardedState = null;
        _sessions.Setup(x => x.Get(It.IsAny<Func<DomainNpcSession, bool>>()))
                 .Returns(
                     [
                         _session,
                         new DomainNpcSession
                         {
                             SessionId = "sess1",
                             NpcId = "other",
                             Persona = new NpcPersona { Name = "Marl" }
                         }
                     ]
                 );
        _brain.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>()))
        .ReturnsAsync(
            new RespondResult
            {
                Text = "hello",
                Mood = "neutral",
                VoiceId = "bm_george"
            }
        );

        var data = TurnData();
        data["newTurns"] = new List<object>
        {
            new Dictionary<string, object>
            {
                ["speakerId"] = "p",
                ["text"] = "Marl, open up",
                ["t"] = 1L
            }
        };

        await _sut.HandleTurnAsync(5006, data);

        _brain.Verify(x => x.RespondAsync(It.Is<RespondRequest>(r => r.MayNotBeAddressed)), Times.Once);
        _brain.Verify(x => x.ClassifyGuardedAsync(It.IsAny<NpcGuardedClassifyRequest>()), Times.Never);
    }

    [Fact]
    public async Task GuardedBorderline_CancelsBeforeClassifier()
    {
        _session = MakeGuardedSession();
        _session.Persona.Name = "Merl";
        _sessions.Setup(x => x.Get(It.IsAny<Func<DomainNpcSession, bool>>()))
                 .Returns(
                     [
                         _session,
                         new DomainNpcSession
                         {
                             SessionId = "sess1",
                             NpcId = "other",
                             Persona = new NpcPersona { Name = "Marl" }
                         }
                     ]
                 );

        var data = TurnData();
        data["newTurns"] = new List<object>
        {
            new Dictionary<string, object>
            {
                ["speakerId"] = "p",
                ["text"] = "Marl, open up",
                ["t"] = 1L
            }
        };

        await _sut.HandleTurnAsync(5006, data);

        _brain.Verify(x => x.ClassifyGuardedAsync(It.IsAny<NpcGuardedClassifyRequest>()), Times.Never);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_turn_cancel"))), Times.Once);
    }

    [Fact]
    public async Task ConcurrentGuardedTurns_DoNotDoubleDiscloseSameFact()
    {
        _session = MakeGuardedSession();
        var gate = new TaskCompletionSource();
        var started = 0;
        var permitted = new List<string>();

        _sessions.Setup(x => x.Update(
                            It.IsAny<System.Linq.Expressions.Expression<Func<DomainNpcSession, bool>>>(),
                            It.IsAny<MongoDB.Driver.UpdateDefinition<DomainNpcSession>>()
                        )
                 )
                 .Callback(() =>
                     {
                         _updates++;
                         // First own-history update of a disclose turn: apply ledger so the next turn sees it.
                         if (_updates == 1 && !_session.GuardedState.DisclosedFactIds.Contains("f1"))
                         {
                             _session.GuardedState.DisclosedFactIds.Add("f1");
                             _session.GuardedState.CooperationBand = NpcCooperationBands.Engaged;
                         }
                     }
                 )
                 .Returns(Task.CompletedTask);

        _brain.Setup(x => x.ClassifyGuardedAsync(It.IsAny<NpcGuardedClassifyRequest>()))
              .Returns(async () =>
                  {
                      var n = System.Threading.Interlocked.Increment(ref started);
                      if (n == 1) await gate.Task;
                      return new NpcGuardedClassifyResult { Classifications = [Tag(NpcGuardedTags.RelevantQuestion, 1)], Ms = 1 };
                  }
              );
        _brain.Setup(x => x.ReplyGuardedAsync(It.IsAny<NpcGuardedReplyRequest>()))
              .Callback<NpcGuardedReplyRequest>(r => permitted.Add(r.PermittedFactId ?? ""))
              .ReturnsAsync(OkReply("ok", "f1"));

        var t1 = _sut.HandleTurnAsync(5006, TurnData(turnId: "t1"));
        await Task.Delay(40);
        var t2 = _sut.HandleTurnAsync(5006, TurnData(turnId: "t2"));
        await Task.Delay(40);
        started.Should().Be(1);
        gate.SetResult();
        await Task.WhenAll(t1, t2);

        permitted.Should().HaveCount(2);
        permitted[0].Should().Be("f1");
        permitted[1].Should().BeEmpty();
    }

    [Fact]
    public async Task GuardedStateCommand_RedactsCanonicalFactsFromClassifierDebug()
    {
        _session = MakeGuardedSession();
        var classification = Tag(NpcGuardedTags.Other);
        classification.Reason = _session.Guarded.Facts[1].Text;
        classification.Evidence = _session.Guarded.Facts[2].Text;
        SetupClassify(classification);
        SetupReply("I won't say more.", "neutral", null, null);

        string stateCommand = null;
        _commands.Setup(x => x.SendCommandAsync(5006, It.Is<string>(command => command.Contains("npc_guarded_state"))))
                 .Callback<int, string>((_, command) => stateCommand = command)
                 .Returns(Task.CompletedTask);

        await _sut.HandleTurnAsync(5006, TurnData());

        stateCommand.Should().Contain("[redacted]");
        stateCommand.Should().NotContain(_session.Guarded.Facts[1].Text);
        stateCommand.Should().NotContain(_session.Guarded.Facts[2].Text);
    }

    [Fact]
    public async Task DisclosedFactRestatement_PassesValidationOnLaterTurn()
    {
        _session = MakeGuardedSession();
        _session.GuardedState.DisclosedFactIds = ["f1"];
        _session.GuardedState.CooperationBand = NpcCooperationBands.Engaged;
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 2));
        SetupReply("As I said earlier — Trucks have been rolling past the farm after dark. About the mill…", "neutral", null, "f2");

        string spoken = null;
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Callback<string, string, string, Func<string, Task>>((_, text, _, _) => spoken = text)
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, TurnData());

        spoken.Should().Contain("Trucks have been rolling past the farm after dark.");
        spoken.Should().Contain("They stop at the old mill by the river bend.");
        _updates.Should().BeGreaterThan(0);
    }
}
