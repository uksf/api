using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class DevRunServiceTests : IDisposable
{
    private readonly Mock<IDevRunLauncher> _launcher = new();
    private readonly Mock<IDevRunsContext> _context = new();
    private readonly Mock<IProcessUtilities> _processUtilities = new();
    private readonly Mock<IArmaSyntheticLaunchGate> _gate = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IUksfLogger> _logger = new();

    private readonly string _tempResultsDir;
    private DomainDevRun _storedRecord;

    public DevRunServiceTests()
    {
        _tempResultsDir = Path.Combine(Path.GetTempPath(), "dev-run-results-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempResultsDir);

        // Default: gate is free.
        _gate.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(true);

        // Default: context.Add captures the record.
        _context.Setup(x => x.Add(It.IsAny<DomainDevRun>()))
                .Callback<DomainDevRun>(r =>
                    {
                        r.Id = "test-mongo-id";
                        _storedRecord = r;
                    }
                )
                .Returns(Task.CompletedTask);

        // Default: context.Update is a no-op (verified per-test as needed).
        _context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, object>>>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_DEV_RUN_RESULTS"))
                         .Returns(new DomainVariableItem { Key = "SERVER_PATH_DEV_RUN_RESULTS", Item = _tempResultsDir });
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempResultsDir))
            {
                Directory.Delete(_tempResultsDir, true);
            }
        }
        catch { }
    }

    private DevRunService BuildService(int pollMs = 50, int timeoutSeconds = 5) =>
        new(_launcher.Object, _context.Object, _processUtilities.Object, _gate.Object, _variablesService.Object, _logger.Object, pollMs, timeoutSeconds);

    private void SetupGetSingle(DomainDevRun record) => _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainDevRun, bool>>())).Returns(record);

    // ─── Trigger tests ───────────────────────────────────────────────────────

    [Fact]
    public void Trigger_returns_AlreadyRunning_when_gate_held()
    {
        _gate.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(false);
        _gate.SetupGet(x => x.CurrentRunId).Returns("existing-run-id");

        var sut = BuildService();
        var result = sut.Trigger("diag_log 1;", [], null);

        result.Outcome.Should().Be(DevRunTriggerOutcome.AlreadyRunning);
        result.RunId.Should().Be("existing-run-id");
        _launcher.Verify(x => x.Launch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public void Trigger_returns_BadModPaths_when_launcher_throws_InvalidModPathException()
    {
        var missingPaths = new List<string> { "/missing/mod1", "/missing/mod2" };
        _launcher.Setup(x => x.Launch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                 .Throws(new InvalidModPathException(missingPaths));

        var sut = BuildService();
        var result = sut.Trigger("diag_log 1;", ["/missing/mod1", "/missing/mod2"], null);

        result.Outcome.Should().Be(DevRunTriggerOutcome.BadModPaths);
        result.MissingPaths.Should().BeEquivalentTo(missingPaths);
        _gate.Verify(x => x.Release(), Times.Once);
    }

    [Fact]
    public void Trigger_persists_initial_record_then_launches()
    {
        _launcher.Setup(x => x.Launch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                 .Returns(new SyntheticLaunchResult(1234, "profile", "mission"));

        var sut = BuildService();
        var result = sut.Trigger("diag_log \"hello\";", ["@mod1"], null);

        result.Outcome.Should().Be(DevRunTriggerOutcome.Started);
        result.RunId.Should().NotBeNullOrEmpty();

        _context.Verify(
            x => x.Add(
                It.Is<DomainDevRun>(r => r.RunId == result.RunId &&
                                         r.Sqf == "diag_log \"hello\";" &&
                                         r.Status == DevRunStatus.Running &&
                                         r.Mods.Count == 1 &&
                                         r.Mods[0] == "@mod1"
                )
            ),
            Times.Once
        );

        _launcher.Verify(x => x.Launch(result.RunId, "diag_log \"hello\";", It.Is<IReadOnlyList<string>>(m => m.Count == 1)), Times.Once);
    }

    // ─── AppendLogAsync tests ─────────────────────────────────────────────────

    [Fact]
    public async Task AppendLogAsync_appends_log_entry()
    {
        var record = new DomainDevRun
        {
            Id = "id1",
            RunId = "run1",
            Logs = []
        };
        SetupGetSingle(record);

        List<DevRunLogEntry> capturedLogs = null;
        _context.Setup(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, List<DevRunLogEntry>>>>(), It.IsAny<List<DevRunLogEntry>>()))
                .Callback<string, Expression<Func<DomainDevRun, List<DevRunLogEntry>>>, List<DevRunLogEntry>>((_, _, v) => capturedLogs = v)
                .Returns(Task.CompletedTask);

        var sut = BuildService();
        await sut.AppendLogAsync("run1", "hello log");

        capturedLogs.Should().NotBeNull();
        capturedLogs.Should().ContainSingle(e => e.Line == "hello log");
    }

    [Fact]
    public async Task AppendLogAsync_truncates_line_over_4096_chars()
    {
        var record = new DomainDevRun
        {
            Id = "id1",
            RunId = "run1",
            Logs = []
        };
        SetupGetSingle(record);

        List<DevRunLogEntry> capturedLogs = null;
        _context.Setup(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, List<DevRunLogEntry>>>>(), It.IsAny<List<DevRunLogEntry>>()))
                .Callback<string, Expression<Func<DomainDevRun, List<DevRunLogEntry>>>, List<DevRunLogEntry>>((_, _, v) => capturedLogs = v)
                .Returns(Task.CompletedTask);

        var longLine = new string('x', 5000);
        var sut = BuildService();
        await sut.AppendLogAsync("run1", longLine);

        capturedLogs.Should().ContainSingle();
        capturedLogs![0].Line.Should().EndWith("[...]");
        capturedLogs[0].Line.Length.Should().BeLessThanOrEqualTo(4096);
    }

    [Fact]
    public async Task AppendLogAsync_caps_at_LogArrayCap_and_sets_LogsTruncated()
    {
        var logs = new List<DevRunLogEntry>();
        for (var i = 0; i < 10_000; i++)
        {
            logs.Add(new DevRunLogEntry(DateTime.UtcNow, $"line {i}"));
        }

        var record = new DomainDevRun
        {
            Id = "id1",
            RunId = "run1",
            Logs = logs,
            LogsTruncated = false
        };
        SetupGetSingle(record);

        bool? capturedTruncated = null;
        _context.Setup(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, bool>>>(), It.IsAny<bool>()))
                .Callback<string, Expression<Func<DomainDevRun, bool>>, bool>((_, _, v) => capturedTruncated = v)
                .Returns(Task.CompletedTask);

        var sut = BuildService();
        await sut.AppendLogAsync("run1", "overflow line");

        capturedTruncated.Should().BeTrue();
        _context.Verify(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, List<DevRunLogEntry>>>>(), It.IsAny<List<DevRunLogEntry>>()), Times.Never);
    }

    // ─── AppendResultAsync tests ──────────────────────────────────────────────

    [Fact]
    public async Task AppendResultAsync_inlines_when_under_1MB()
    {
        var record = new DomainDevRun { Id = "id1", RunId = "run1" };
        SetupGetSingle(record);

        string capturedResult = null;
        _context.Setup(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, string>>>(), It.IsAny<string>()))
                .Callback<string, Expression<Func<DomainDevRun, string>>, string>((_, _, v) => capturedResult = v)
                .Returns(Task.CompletedTask);

        var smallPayload = new string('a', 100);
        var sut = BuildService();
        await sut.AppendResultAsync("run1", smallPayload);

        capturedResult.Should().Be(smallPayload);
        // No file should have been created in the results dir.
        Directory.EnumerateFiles(_tempResultsDir).Should().BeEmpty();
    }

    [Fact]
    public async Task AppendResultAsync_writes_to_disk_when_over_1MB_under_14MB()
    {
        var record = new DomainDevRun { Id = "id1", RunId = "run1" };
        SetupGetSingle(record);

        string capturedFilePath = null;
        _context.Setup(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, string>>>(), It.IsAny<string>()))
                .Callback<string, Expression<Func<DomainDevRun, string>>, string>((_, _, v) => capturedFilePath = v)
                .Returns(Task.CompletedTask);

        // 2 MB payload — over the 1 MB inline threshold, under 14 MB disk cap.
        var payload = new string('x', 2 * 1_048_576);
        var sut = BuildService();
        await sut.AppendResultAsync("run1", payload);

        var expectedPath = Path.Combine(_tempResultsDir, "run1.txt");
        capturedFilePath.Should().Be(expectedPath);
        File.Exists(expectedPath).Should().BeTrue();
        (new FileInfo(expectedPath).Length).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AppendResultAsync_rejects_over_14MB_with_FailedTooLarge()
    {
        var record = new DomainDevRun
        {
            Id = "id1",
            RunId = "run1",
            Status = DevRunStatus.Running
        };
        SetupGetSingle(record);

        DevRunStatus? capturedStatus = null;
        _context.Setup(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, DevRunStatus>>>(), It.IsAny<DevRunStatus>()))
                .Callback<string, Expression<Func<DomainDevRun, DevRunStatus>>, DevRunStatus>((_, _, v) => capturedStatus = v)
                .Returns(Task.CompletedTask);

        // 15 MB payload — exceeds the 14 MB disk cap.
        var payload = new string('z', 15 * 1_048_576);
        var sut = BuildService();
        await sut.AppendResultAsync("run1", payload);

        capturedStatus.Should().Be(DevRunStatus.FailedTooLarge);
    }

    // ─── FinishAsync tests ────────────────────────────────────────────────────

    [Fact]
    public async Task FinishAsync_sets_Success_and_CompletedAt()
    {
        var record = new DomainDevRun
        {
            Id = "id1",
            RunId = "run1",
            Status = DevRunStatus.Running
        };
        SetupGetSingle(record);

        DevRunStatus? capturedStatus = null;
        DateTime? capturedCompletedAt = null;

        _context.Setup(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, DevRunStatus>>>(), It.IsAny<DevRunStatus>()))
                .Callback<string, Expression<Func<DomainDevRun, DevRunStatus>>, DevRunStatus>((_, _, v) => capturedStatus = v)
                .Returns(Task.CompletedTask);

        _context.Setup(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, DateTime?>>>(), It.IsAny<DateTime?>()))
                .Callback<string, Expression<Func<DomainDevRun, DateTime?>>, DateTime?>((_, _, v) => capturedCompletedAt = v)
                .Returns(Task.CompletedTask);

        var before = DateTime.UtcNow;
        var sut = BuildService();
        await sut.FinishAsync("run1");

        capturedStatus.Should().Be(DevRunStatus.Success);
        capturedCompletedAt.Should().NotBeNull();
        capturedCompletedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task FinishAsync_kills_tracked_process_so_watcher_releases_gate()
    {
        // Trigger registers the pid in the active map.
        _launcher.Setup(x => x.Launch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                 .Returns(new SyntheticLaunchResult(4321, "profile", "mission"));
        // Keep watcher alive long enough that finish is the path that kills the process.
        _processUtilities.Setup(x => x.IsProcessAlive(It.IsAny<int>())).Returns(true);

        var sut = BuildService(pollMs: 50, timeoutSeconds: 60);
        var trigger = sut.Trigger("diag_log 1;", ["@mod"], null);

        // Record now exists and is Running.
        var record = new DomainDevRun
        {
            Id = "id1",
            RunId = trigger.RunId,
            Status = DevRunStatus.Running
        };
        SetupGetSingle(record);

        await sut.FinishAsync(trigger.RunId);

        _processUtilities.Verify(x => x.FindProcessById(4321), Times.Once);
    }

    // ─── GetStatus tests ──────────────────────────────────────────────────────

    [Fact]
    public void GetStatus_returns_null_when_record_missing()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainDevRun, bool>>())).Returns((DomainDevRun)null);

        var sut = BuildService();
        var result = sut.GetStatus("nonexistent-run");

        result.Should().BeNull();
    }

    [Fact]
    public void GetStatus_includes_first_256_chars_of_Result()
    {
        var longResult = new string('r', 500);
        var record = new DomainDevRun
        {
            Id = "id1",
            RunId = "run1",
            Status = DevRunStatus.Success,
            Result = longResult
        };
        SetupGetSingle(record);

        var sut = BuildService();
        var response = sut.GetStatus("run1");

        response.Should().NotBeNull();
        response!.ResultPreview.Should().HaveLength(256);
        response.ResultPreview.Should().Be(longResult[..256]);
    }

    [Fact]
    public void GetStatus_returns_null_ResultPreview_when_Result_is_null()
    {
        var record = new DomainDevRun
        {
            Id = "id1",
            RunId = "run1",
            Status = DevRunStatus.Running,
            Result = null
        };
        SetupGetSingle(record);

        var sut = BuildService();
        var response = sut.GetStatus("run1");

        response!.ResultPreview.Should().BeNull();
    }
}
