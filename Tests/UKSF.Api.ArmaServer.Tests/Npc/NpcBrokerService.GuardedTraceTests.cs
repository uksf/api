using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.ArmaServer.Tests.Npc.Observability;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceGuardedTests
{
    private NpcBrokerService Traced(CapturingTraceRecorder trace) =>
        new(
            _sessions.Object,
            _clips.Object,
            _brain.Object,
            _clacks.Object,
            _commands.Object,
            _audio.Object,
            _voiceStore.Object,
            _voices.Object,
            _vars.Object,
            _logger.Object,
            trace
        );

    [Fact]
    public async Task GuardedTurn_TracesEngineStateAndOutcome()
    {
        var trace = new CapturingTraceRecorder();
        _session = MakeGuardedSession();
        SetupClassify(Tag(NpcGuardedTags.Threat));
        SetupReply("Don't. Threaten my family again.", "afraid", "steps back", null);

        await Traced(trace).HandleTurnAsync(5006, TurnData());

        trace.Events.Select(e => e.Type).Should().Equal("turn.decided", "turn.replied", "turn.finished");
        var decided = trace.Single("turn.decided").Data["decision"];
        decided["directive"].AsString.Should().Be(NpcGuardedDirectives.Warn);
        decided["before"]["pendingWarning"].AsBoolean.Should().BeFalse();
        decided["after"]["pendingWarning"].AsBoolean.Should().BeTrue();
        trace.Single("turn.replied").Data["reply"]["spoken"].AsString.Should().Be("Don't. Threaten my family again.");
        var finished = trace.Single("turn.finished").Data;
        finished["outcome"].AsString.Should().Be("spoke");
        finished["committed"].AsBoolean.Should().BeTrue();
        finished["emoteSent"].AsBoolean.Should().BeTrue();
    }
}
