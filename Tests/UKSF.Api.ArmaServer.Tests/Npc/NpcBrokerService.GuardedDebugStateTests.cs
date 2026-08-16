using System;
using System.Linq;
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
    public async Task HandleTurnAsync_GuardedAnswer_SendsDebugStateWithTagIdsAndProvider_NoFactText()
    {
        _session = MakeGuardedSession();
        var reply = OkReply("I noticed some traffic.", "f1");
        reply.Provider = "luna@ultron";
        _lastClassify = [Tag(NpcGuardedTags.RelevantQuestion, 1)];
        _lastReply = reply;
        WireTurn();

        await _sut.HandleTurnAsync(5006, TurnData());

        _commands.Verify(
            x => x.SendCommandAsync(
                5006,
                It.Is<string>(c => c.Contains("\"npc_debug_state\"") &&
                                   c.Contains("\"luna@ultron\"") &&
                                   c.Contains("\"relevant_question\"") &&
                                   c.Contains("\"f1\"") &&
                                   !c.Contains("Trucks have been rolling")
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleTurnAsync_GuardedZeroFrames_SendsDebugState_LedgerUnchanged()
    {
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("I noticed some traffic.", "neutral", null, "f1");
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Returns(Task.CompletedTask);

        await _sut.HandleTurnAsync(5006, TurnData());

        _updates.Should().Be(0);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_turn_cancel") && c.Contains("turn7"))), Times.Once);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("\"npc_debug_state\""))), Times.Once);
    }

    [Fact]
    public async Task HandleTurnAsync_GuardedNullClassifier_SendsDebugState_NoCommit()
    {
        _session = MakeGuardedSession();
        _brain.Setup(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()))
              .ReturnsAsync(new NpcGuardedTurnResult { Classify = null, Reply = new NpcGuardedReplyResult { Ok = false, Failure = "null model" } });

        await _sut.HandleTurnAsync(5006, TurnData());

        _updates.Should().Be(0);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("\"npc_debug_state\"") && c.Contains("\"answer\""))), Times.Once);
    }

    [Fact]
    public async Task HandleTurnAsync_GuardedReasonWithCanonicalText_RedactedInDebugState()
    {
        _session = MakeGuardedSession();
        var leak = Tag(NpcGuardedTags.RelevantQuestion, 1);
        leak.Reason = "Player asked. Trucks have been rolling past the farm after dark.";
        leak.Evidence = "trucks";
        SetupClassify(leak);
        SetupReply("I noticed some traffic.", "neutral", null, "f1");

        await _sut.HandleTurnAsync(5006, TurnData());

        var debugCommands = _commands.Invocations
                                     .Where(i => i.Method.Name == nameof(IGameServerCommandSender.SendCommandAsync) &&
                                                 i.Arguments[1] is string c &&
                                                 c.Contains("\"npc_debug_state\"")
                                     )
                                     .Select(i => (string)i.Arguments[1])
                                     .ToList();
        debugCommands.Should().HaveCount(1);
        debugCommands[0].Should().NotContain("Trucks have been rolling");
        debugCommands[0].Should().Contain("[redacted]");
    }

    [Fact]
    public async Task HandleTurnAsync_GuardedReasonWithConcernText_RedactedInDebugState()
    {
        _session = MakeGuardedSession();
        var leak = Tag(NpcGuardedTags.RelevantQuestion, 1);
        leak.Reason = "He mentioned retaliation against family";
        leak.Evidence = "family";
        SetupClassify(leak);
        SetupReply("I noticed some traffic.", "neutral", null, "f1");

        await _sut.HandleTurnAsync(5006, TurnData());

        var debugCommands = _commands.Invocations
                                     .Where(i => i.Method.Name == nameof(IGameServerCommandSender.SendCommandAsync) &&
                                                 i.Arguments[1] is string c &&
                                                 c.Contains("\"npc_debug_state\"")
                                     )
                                     .Select(i => (string)i.Arguments[1])
                                     .ToList();
        debugCommands.Should().HaveCount(1);
        debugCommands[0].Should().NotContain("retaliation against family");
        debugCommands[0].Should().Contain("[redacted]");
    }

    [Fact]
    public async Task HandleTurnAsync_GuardedSessionVanishes_SendsDebugStateStaySilent()
    {
        var remaining = 1;
        _session = MakeGuardedSession();
        _sessions.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(() => remaining-- > 0 ? _session : null);

        await _sut.HandleTurnAsync(5006, TurnData());

        _brain.Verify(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()), Times.Never);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_turn_cancel") && c.Contains("turn7"))), Times.Once);
        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("\"npc_debug_state\"") && c.Contains("\"stay_silent\""))), Times.Once);
    }

    [Fact]
    public async Task HandleTurnAsync_GuardedCommitTargetMissing_SendsDebugState()
    {
        var remaining = 2;
        _session = MakeGuardedSession();
        _sessions.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(() => remaining-- > 0 ? _session : null);
        SetupClassify(Tag(NpcGuardedTags.RelevantQuestion, 1));
        SetupReply("I noticed some traffic.", "neutral", null, "f1");

        await _sut.HandleTurnAsync(5006, TurnData());

        _commands.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("\"npc_debug_state\""))), Times.Once);
        _logger.Verify(x => x.LogWarning(It.Is<string>(m => m.Contains("commit target missing"))), Times.Once);
    }
}
