using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceGuardedTests
{
    [Fact]
    public async Task CleanGuardedTurn_ClassifiesRepliesStreamsAndCommits()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("I noticed some traffic.", "neutral", null, "f1");

        await _sut.HandleTurnAsync(5006, TurnData());

        _brain.Verify(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()), Times.Once);
        _brain.Verify(x => x.ClassifyGuardedAsync(It.IsAny<NpcGuardedClassifyRequest>()), Times.Never);
        _brain.Verify(x => x.ReplyGuardedAsync(It.IsAny<NpcGuardedReplyRequest>()), Times.Never);
        _brain.Verify(x => x.RespondAsync(It.IsAny<RespondRequest>()), Times.Never);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_audio_frame"))), Times.AtLeastOnce);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_guarded_state"))), Times.Once);
        _updates.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EarlyFactRequest_GivesReplyNoFact()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 3));
        SetupReply("Not sure about that.", "neutral", null, null);
        string spoken = null;
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Callback<string, string, string, Func<string, Task>>((_, text, _, _) => spoken = text)
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, TurnData());

        spoken.Should().Be("Not sure about that.");
        spoken.Should().NotContain("midnight");
    }

    [Fact]
    public async Task NullClassifier_SafeDeflection_NoRetry_NoCommit()
    {
        _session = MakeGuardedSession();
        _brain.Setup(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()))
              .ReturnsAsync(new NpcGuardedTurnResult { Classify = null, Reply = new NpcGuardedReplyResult { Ok = false, Failure = "null model" } });

        await _sut.HandleTurnAsync(5006, TurnData());

        _brain.Verify(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()), Times.Once);
        _updates.Should().Be(0);
        _clacks.Verify(
            x => x.SpeakStreamAsync(It.IsAny<string>(), NpcGuardedProfile.SafeDeflection, It.IsAny<string>(), It.IsAny<Func<string, Task>>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ThrowingReply_SafeDeflection_NoRetry()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.Other));
        SetupTurnThrow(new InvalidOperationException("boom"));

        await _sut.HandleTurnAsync(5006, TurnData());

        _brain.Verify(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()), Times.Once);
        _clacks.Verify(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()), Times.Once);
    }

    [Fact]
    public async Task CanonicalTextWithoutId_Rejected_UsesFallback()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("Trucks have been rolling past the farm after dark.", "neutral", null, null);

        await _sut.HandleTurnAsync(5006, TurnData());

        _clacks.Verify(
            x => x.SpeakStreamAsync(
                It.IsAny<string>(),
                It.Is<string>(t => t.Contains("Trucks have been rolling")),
                It.IsAny<string>(),
                It.IsAny<Func<string, Task>>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task MatchingId_ComposesCanonicalInStreamedText()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("Listen carefully.", "neutral", null, "f1");
        string spoken = null;
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Callback<string, string, string, Func<string, Task>>((_, text, _, _) => spoken = text)
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, TurnData());

        spoken.Should().Contain("Listen carefully.");
        spoken.Should().Contain("Trucks have been rolling past the farm after dark.");
    }

    [Fact]
    public async Task ZeroFrameTts_LeavesStateUnchanged()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("Listen carefully.", "neutral", null, "f1");
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Returns(Task.CompletedTask);

        await _sut.HandleTurnAsync(5006, TurnData());

        _updates.Should().Be(0);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_guarded_state"))), Times.Never);
    }

    [Fact]
    public async Task GuardedOtherNpcNamed_CancelsBeforeClassifier()
    {
        _session = MakeGuardedSession();
        _sessions.Setup(x => x.Get(It.IsAny<Func<DomainNpcSession, bool>>()))
                 .Returns(
                     [
                         _session,
                         new DomainNpcSession
                         {
                             SessionId = "sess1",
                             NpcId = "other",
                             Persona = new NpcPersona { Name = "Pavel" }
                         }
                     ]
                 );
        var data = TurnData();
        data["newTurns"] = new List<object>
        {
            new Dictionary<string, object>
            {
                ["speakerId"] = "p",
                ["text"] = "Hey Pavel, over here",
                ["t"] = 1L
            }
        };

        await _sut.HandleTurnAsync(5006, data);

        _brain.Verify(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()), Times.Never);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_turn_cancel") && c.Contains("turn7"))), Times.Once);
    }

    [Fact]
    public async Task ConcurrentGuardedTurns_SerializeViaLock()
    {
        _session = MakeGuardedSession();
        var gate = new TaskCompletionSource();
        var started = 0;
        _lastClassify = [Tag(NpcGuardedTags.RelevantQuestion, 1)];
        _lastReply = OkReply("ok", "f1");
        _brain.Setup(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()))
              .Returns(async () =>
                  {
                      var n = System.Threading.Interlocked.Increment(ref started);
                      if (n == 1) await gate.Task;
                      return new NpcGuardedTurnResult
                      {
                          Classify = new NpcGuardedClassifyResult { Classifications = [Tag(NpcGuardedTags.RelevantQuestion, 1)], Ms = 1 }, Reply = _lastReply
                      };
                  }
              );

        var t1 = _sut.HandleTurnAsync(5006, TurnData(turnId: "t1"));
        await Task.Delay(50);
        var t2 = _sut.HandleTurnAsync(5006, TurnData(turnId: "t2"));
        await Task.Delay(50);
        started.Should().Be(1);
        gate.SetResult();
        await Task.WhenAll(t1, t2);
        started.Should().Be(2);
    }

    [Fact]
    public async Task MissionCleanupDuringTurn_LogsMissingCommitTarget()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("Listen carefully.", "neutral", null, "f1");
        var calls = 0;
        _sessions.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>()))
                 .Returns(() =>
                     {
                         calls++;
                         return calls <= 2 ? _session : null;
                     }
                 );

        await _sut.HandleTurnAsync(5006, TurnData());

        _logger.Verify(x => x.LogWarning(It.Is<string>(m => m.Contains("commit target missing"))), Times.Once);
    }
}
