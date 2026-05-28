using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class SteamCmdServiceTests
{
    [Theory]
    [InlineData("Logging in user 'x' to Steam Public...ERROR (Two-factor code mismatch)")]
    [InlineData("Logging in user 'x'...ERROR (Account Logon Denied)")]
    [InlineData("two-factor CODE MISMATCH")]
    public void IsTransientLoginFailure_ForCodeRejection_ReturnsTrue(string output)
    {
        SteamCmdService.IsTransientLoginFailure(output).Should().BeTrue();
    }

    [Theory]
    [InlineData("Logging in user 'x' to Steam Public...OK\nWaiting for user info...OK")]
    [InlineData("Success! Downloaded item.")]
    [InlineData("")]
    [InlineData(null)]
    public void IsTransientLoginFailure_ForSuccessOrUnrelated_ReturnsFalse(string output)
    {
        SteamCmdService.IsTransientLoginFailure(output).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteWithCodeRetry_WhenFirstAttemptSucceeds_DoesNotRetry()
    {
        var calls = 0;
        var retries = 0;

        var result = await SteamCmdService.ExecuteWithCodeRetry(
            () =>
            {
                calls++;
                return Task.FromResult("Logging in user 'x'...OK");
            },
            () => TimeSpan.Zero,
            _ => Task.CompletedTask,
            codeConfigured: true,
            _ => retries++
        );

        result.Should().Contain("OK");
        calls.Should().Be(1);
        retries.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteWithCodeRetry_WhenCodeRejectedThenSucceeds_RetriesWithFreshCode()
    {
        var outputs = new Queue<string>(["ERROR (Two-factor code mismatch)", "Logging in user 'x'...OK"]);
        var calls = 0;
        var retries = 0;

        var result = await SteamCmdService.ExecuteWithCodeRetry(
            () =>
            {
                calls++;
                return Task.FromResult(outputs.Dequeue());
            },
            () => TimeSpan.Zero,
            _ => Task.CompletedTask,
            codeConfigured: true,
            _ => retries++
        );

        result.Should().Contain("OK");
        calls.Should().Be(2);
        retries.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithCodeRetry_WhenCodeRejectedPersistently_StopsAtMaxAttempts()
    {
        var calls = 0;

        var result = await SteamCmdService.ExecuteWithCodeRetry(
            () =>
            {
                calls++;
                return Task.FromResult("ERROR (Two-factor code mismatch)");
            },
            () => TimeSpan.Zero,
            _ => Task.CompletedTask,
            codeConfigured: true,
            _ => { }
        );

        result.Should().Contain("Two-factor code mismatch");
        calls.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteWithCodeRetry_WhenNoCodeConfigured_DoesNotRetry()
    {
        var calls = 0;

        await SteamCmdService.ExecuteWithCodeRetry(
            () =>
            {
                calls++;
                return Task.FromResult("ERROR (Account Logon Denied)");
            },
            () => TimeSpan.Zero,
            _ => Task.CompletedTask,
            codeConfigured: false,
            _ => { }
        );

        calls.Should().Be(1);
    }
}
