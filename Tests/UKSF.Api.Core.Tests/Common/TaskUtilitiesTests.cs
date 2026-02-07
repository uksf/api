using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.Core.Extensions;
using Xunit;

namespace UKSF.Api.Core.Tests.Common;

public class TaskUtilitiesTests
{
    [Fact]
    public async Task Delay_ShouldNotThrow_WhenCancelled()
    {
        CancellationTokenSource token = new();
        token.Cancel();

        var act = () => TaskUtilities.Delay(TimeSpan.FromMilliseconds(50), token.Token);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DelayWithCallback_ShouldInvokeCallback_AfterDelay()
    {
        var callbackInvoked = false;
        CancellationTokenSource token = new();

        await TaskUtilities.DelayWithCallback(
            TimeSpan.FromMilliseconds(10),
            token.Token,
            () =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }
        );

        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task DelayWithCallback_ShouldNotInvokeCallback_WhenCancelled()
    {
        var callbackInvoked = false;
        CancellationTokenSource token = new();
        token.Cancel();

        await TaskUtilities.DelayWithCallback(
            TimeSpan.FromMilliseconds(50),
            token.Token,
            () =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }
        );

        callbackInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task DelayWithCallback_ShouldPropagateCallbackException()
    {
        CancellationTokenSource token = new();

        var act = () => TaskUtilities.DelayWithCallback(
            TimeSpan.FromMilliseconds(1),
            token.Token,
            () => throw new InvalidOperationException("callback failed")
        );

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("callback failed");
    }
}
