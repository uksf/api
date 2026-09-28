using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class PasskeyFlowStoreTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly PasskeyFlow _flow = new("{}", null, null);

    [Fact]
    public void A_flow_can_be_taken_once()
    {
        var subject = new PasskeyFlowStore(_time);
        var flowId = subject.Add(_flow);

        subject.Take(flowId).Should().Be(_flow);
        var act = () => subject.Take(flowId);

        act.Should().Throw<BadRequestException>().WithMessage("Passkey request expired*");
    }

    [Fact]
    public void Only_one_of_many_concurrent_takes_succeeds()
    {
        var subject = new PasskeyFlowStore(_time);
        var flowId = subject.Add(_flow);

        var results = Enumerable.Range(0, 32)
                                .AsParallel()
                                .Select(_ =>
                                    {
                                        try
                                        {
                                            subject.Take(flowId);
                                            return true;
                                        }
                                        catch (BadRequestException)
                                        {
                                            return false;
                                        }
                                    }
                                )
                                .ToList();

        results.Count(x => x).Should().Be(1);
    }

    [Fact]
    public void An_expired_flow_cannot_be_taken()
    {
        var subject = new PasskeyFlowStore(_time);
        var flowId = subject.Add(_flow);
        _time.Advance(TimeSpan.FromMinutes(11));

        var act = () => subject.Take(flowId);

        act.Should().Throw<BadRequestException>();
    }

    [Fact]
    public void A_full_store_drops_expired_flows_to_make_room()
    {
        var subject = new PasskeyFlowStore(_time, 2);
        subject.Add(_flow);
        subject.Add(_flow);
        _time.Advance(TimeSpan.FromMinutes(11));

        var act = () => subject.Add(_flow);

        act.Should().NotThrow();
    }

    [Fact]
    public void A_store_full_of_live_flows_refuses_more()
    {
        var subject = new PasskeyFlowStore(_time, 2);
        subject.Add(_flow);
        subject.Add(_flow);

        var act = () => subject.Add(_flow);

        act.Should().Throw<UksfException>().Where(x => x.StatusCode == 429);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
