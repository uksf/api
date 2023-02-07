using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.Core.Extensions;
using Xunit;

namespace UKSF.Tests.Unit.Common;

public class TaskUtilitiesTests
{
    [Fact]
    public async Task ShouldCallbackAfterDelay()
    {
        var subject = false;
        var act = async () =>
        {
            CancellationTokenSource token = new();
            await TaskUtilities.DelayWithCallback(
                TimeSpan.FromMilliseconds(10),
                token.Token,
                () =>
                {
                    subject = true;
                    return Task.CompletedTask;
                }
            );
        };

        await act.Should().NotThrowAsync();
        act.ExecutionTime().Should().BeGreaterThan(TimeSpan.FromMilliseconds(10));
        subject.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotCallbackForCancellation()
    {
        CancellationTokenSource token = new();
        var act = async () => { await TaskUtilities.DelayWithCallback(TimeSpan.FromMilliseconds(10), token.Token, null); };

        act.Should().NotThrowAsync();
        token.Cancel();
        act.ExecutionTime().Should().BeLessThan(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void ShouldNotThrowExceptionForDelay()
    {
        var act = () =>
        {
            CancellationTokenSource token = new();
            var unused = TaskUtilities.Delay(TimeSpan.FromMilliseconds(50), token.Token);
            token.Cancel();
        };

        act.Should().NotThrow();
    }
}
