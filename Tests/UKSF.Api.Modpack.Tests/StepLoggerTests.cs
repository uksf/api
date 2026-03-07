using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class StepLoggerTests
{
    private readonly ModpackBuildStep _buildStep;
    private readonly StepLogger _stepLogger;

    public StepLoggerTests()
    {
        _buildStep = new ModpackBuildStep("Build UKSF") { Logs = new List<ModpackBuildStepLogItem>() };
        _stepLogger = new StepLogger(_buildStep);
    }

    [Fact]
    public void LogStart_Should_LogStepName()
    {
        _stepLogger.LogStart();

        _buildStep.Logs.Should().ContainSingle();
        _buildStep.Logs[0].Text.Should().Be("Starting: Build UKSF");
    }

    [Fact]
    public void LogSuccess_Should_LogFinishedMessage()
    {
        _buildStep.BuildResult = ModpackBuildResult.Success;

        _stepLogger.LogSuccess();

        _buildStep.Logs.Should().Contain(log => log.Text.Contains("Finished: Build UKSF"));
        _buildStep.Logs.Should().Contain(log => log.Colour == "green");
    }

    [Fact]
    public void LogSuccess_Should_LogWarningFinished_When_BuildResultIsWarning()
    {
        _buildStep.BuildResult = ModpackBuildResult.Warning;

        _stepLogger.LogSuccess();

        _buildStep.Logs.Should().Contain(log => log.Text.Contains("Finished with warning: Build UKSF"));
        _buildStep.Logs.Should().Contain(log => log.Colour == "orangered");
    }

    [Fact]
    public void LogCancelled_Should_LogCancelledMessage()
    {
        _stepLogger.LogCancelled();

        _buildStep.Logs.Should().Contain(log => log.Text == "Build cancelled");
        _buildStep.Logs.Should().Contain(log => log.Colour == "goldenrod");
    }

    [Fact]
    public void LogSkipped_Should_LogSkippedMessage()
    {
        _stepLogger.LogSkipped();

        _buildStep.Logs.Should().Contain(log => log.Text.Contains("Skipped: Build UKSF"));
        _buildStep.Logs.Should().Contain(log => log.Colour == "gray");
    }

    [Fact]
    public void LogWarning_Should_LogWarningWithMessage()
    {
        _stepLogger.LogWarning("Something went wrong");

        _buildStep.Logs.Should().Contain(log => log.Text == "Warning");
        _buildStep.Logs.Should().Contain(log => log.Text == "Something went wrong");
        _buildStep.Logs.Should().OnlyContain(log => log.Colour == "orangered" || log.Colour == "");
    }

    [Fact]
    public void LogError_Should_LogErrorWithFailedMessage()
    {
        var exception = new System.Exception("Something broke");

        _stepLogger.LogError(exception);

        _buildStep.Logs.Should().Contain(log => log.Text == "Error");
        _buildStep.Logs.Should().Contain(log => log.Text.Contains("Something broke"));
        _buildStep.Logs.Should().Contain(log => log.Text == "Failed: Build UKSF");
        _buildStep.Logs.Should().OnlyContain(log => log.Colour == "red" || log.Colour == "");
    }

    [Fact]
    public void LogErrorContent_Should_LogAsRedText_WithoutErrorOrFailedWrapper()
    {
        _stepLogger.LogErrorContent("error[SPE2]: SQF Syntax could not be parsed");

        _buildStep.Logs.Should().ContainSingle();
        _buildStep.Logs[0].Text.Should().Be("error[SPE2]: SQF Syntax could not be parsed");
        _buildStep.Logs[0].Colour.Should().Be("red");

        // Must NOT contain Error/Failed wrapper lines
        _buildStep.Logs.Should().NotContain(log => log.Text == "Error");
        _buildStep.Logs.Should().NotContain(log => log.Text.Contains("Failed:"));
    }

    [Fact]
    public void LogErrorContent_Should_LogMultipleLines_WithoutRepeatedWrappers()
    {
        var errorLines = new[]
        {
            "error[SPE2]: SQF Syntax could not be parsed",
            "    ┌─ addons/blastoverpressure/functions/fnc_waveProcessRays.sqf:160:12",
            "    │",
            "    │     TRACE_0(\"Wave simulation complete\");",
            "    │            ^ unparseable syntax",
            "    │"
        };

        foreach (var line in errorLines)
        {
            _stepLogger.LogErrorContent(line);
        }

        _buildStep.Logs.Should().HaveCount(6);
        _buildStep.Logs.Should().OnlyContain(log => log.Colour == "red");

        // Must NOT contain any Error/Failed wrapper lines
        _buildStep.Logs.Should().NotContain(log => log.Text == "Error");
        _buildStep.Logs.Should().NotContain(log => log.Text.Contains("Failed:"));
    }

    [Fact]
    public void Log_Should_LogWithColour()
    {
        _stepLogger.Log("test message", "blue");

        _buildStep.Logs.Should().ContainSingle();
        _buildStep.Logs[0].Text.Should().Be("test message");
        _buildStep.Logs[0].Colour.Should().Be("blue");
    }

    [Fact]
    public void Log_Should_LogWithoutColour_ByDefault()
    {
        _stepLogger.Log("test message");

        _buildStep.Logs.Should().ContainSingle();
        _buildStep.Logs[0].Text.Should().Be("test message");
        _buildStep.Logs[0].Colour.Should().Be("");
    }

    [Fact]
    public void Log_Should_SplitMultilineText()
    {
        _stepLogger.Log("line1\nline2\nline3", "blue");

        _buildStep.Logs.Should().HaveCount(3);
        _buildStep.Logs[0].Text.Should().Be("line1");
        _buildStep.Logs[1].Text.Should().Be("line2");
        _buildStep.Logs[2].Text.Should().Be("line3");
    }

    [Fact]
    public void LogSurround_Should_LogWithCadetblue()
    {
        _stepLogger.LogSurround("surrounding text");

        _buildStep.Logs.Should().ContainSingle();
        _buildStep.Logs[0].Text.Should().Be("surrounding text");
        _buildStep.Logs[0].Colour.Should().Be("cadetblue");
    }

    [Fact]
    public void LogInline_Should_ReplaceLastLog()
    {
        _stepLogger.Log("first line");
        _stepLogger.LogInline("updated line");

        _buildStep.Logs.Should().ContainSingle();
        _buildStep.Logs[0].Text.Should().Be("updated line");
    }

    [Fact]
    public void LogInline_Should_AddLog_WhenNoExistingLogs()
    {
        _stepLogger.LogInline("first inline");

        _buildStep.Logs.Should().ContainSingle();
        _buildStep.Logs[0].Text.Should().Be("first inline");
    }

    [Fact]
    public async Task ConcurrentLogAndEnumerate_Should_NotThrow()
    {
        // Reproduces the production bug: "Collection was modified; enumeration operation may not execute"
        // when SignalR/MongoDB serializes the Logs list while StepLogger is writing to it.
        //
        // StepLogger's _logLock protects writers from each other, but readers (serializers)
        // enumerate Logs without the lock, so AddRange during enumeration throws.
        //
        // To reliably reproduce: use a ManualResetEvent to guarantee that a write happens
        // while an enumerator is held open — the exact condition that triggers the bug.
        var enumeratorOpened = new ManualResetEventSlim(false);
        var writeCompleted = new ManualResetEventSlim(false);

        // Seed some logs so the enumerator has items to iterate
        for (var i = 0; i < 100; i++)
        {
            _stepLogger.Log($"seed line {i}");
        }

        // Thread 1: open an enumerator (simulating what serializers do), then signal for write
        var readerTask = Task.Run(() =>
            {
                using var enumerator = _buildStep.Logs.GetEnumerator();
                enumerator.MoveNext(); // position on first element
                enumeratorOpened.Set(); // signal that enumerator is open
                writeCompleted.Wait(TimeSpan.FromSeconds(5)); // wait for write to happen
                enumerator.MoveNext(); // this should throw if list was modified
            }
        );

        // Thread 2: wait for enumerator to be open, then write a log
        var writerTask = Task.Run(() =>
            {
                enumeratorOpened.Wait(TimeSpan.FromSeconds(5));
                _stepLogger.Log("concurrent write while enumerator is open");
                writeCompleted.Set();
            }
        );

        var act = () => Task.WhenAll(readerTask, writerTask);

        await act.Should().NotThrowAsync("concurrent enumeration of Logs during writes must not throw");
    }
}
