using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

public partial class GameDataExportServiceTests
{
    private readonly Mock<IGameDataExportProcessLauncher> _launcher = new();
    private readonly Mock<IGameDataExportsContext> _context = new();
    private readonly Mock<IProcessUtilities> _processUtilities = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly Mock<IArmaSyntheticLaunchGate> _gate = new();

    private readonly List<DomainGameDataExport> _persistedRecords = new();

    public GameDataExportServiceTests()
    {
        _context.Setup(x => x.Add(It.IsAny<DomainGameDataExport>()))
                .Callback<DomainGameDataExport>(record => _persistedRecords.Add(record))
                .Returns(Task.CompletedTask);

        _gate.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(true);
    }

    private GameDataExportService CreateSut()
    {
        return new GameDataExportService(_launcher.Object, _context.Object, _processUtilities.Object, _variablesService.Object, _logger.Object, _gate.Object);
    }

    private GameDataExportService CreateFastSut()
    {
        return new GameDataExportService(
            _launcher.Object,
            _context.Object,
            _processUtilities.Object,
            _variablesService.Object,
            _logger.Object,
            _gate.Object,
            pollMs: 100,
            timeoutSeconds: 5
        );
    }

    private GameDataExportService CreateSutWithFastTimeouts(int pollMs, int timeoutSeconds)
    {
        return new GameDataExportService(
            _launcher.Object,
            _context.Object,
            _processUtilities.Object,
            _variablesService.Object,
            _logger.Object,
            _gate.Object,
            pollMs: pollMs,
            timeoutSeconds: timeoutSeconds
        );
    }

    private static DomainVariableItem CreateVariable(string key, object item) => new() { Key = key, Item = item };

    private void SetupVariable(string key, string value)
    {
        _variablesService.Setup(x => x.GetVariable(key)).Returns(CreateVariable(key, value));
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "uksf-gde-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static GameDataExportLaunchResult LaunchResult(int processId, string outDir, string version) =>
        new(
            processId,
            outDir,
            ConfigGlob: $"config_*_uksf-{version}.cpp",
            CbaSettingsGlob: $"cba_settings_*_uksf-{version}.sqf",
            CbaSettingsReferenceGlob: $"cba_settings_reference_*_uksf-{version}.json"
        );

    private async Task<DomainGameDataExport> WaitForPersistedRecord(int maxWaitMs = 5000)
    {
        DomainGameDataExport record = null;
        for (var i = 0; i < maxWaitMs / 50; i++)
        {
            await Task.Delay(50);
            if (_persistedRecords.Count > 0)
            {
                record = _persistedRecords.Last();
                break;
            }
        }

        record.Should().NotBeNull("waited {0}ms but no DomainGameDataExport was persisted via _context.Add", maxWaitMs);
        return record!;
    }

    private static string CompleteConfig(int padBytes) => "class CfgPatches {\n\tpad = \"" + new string('x', padBytes) + "\";\n};\n";

    private static string TruncatedConfig(int padBytes) =>
        "class CfgPatches {\n\tpad = \"" + new string('x', padBytes) + "\";\n\tclass CommunicationsMedium {\n";

    // ─── Gate / status tests ─────────────────────────────────────────────────

    [Fact]
    public void Trigger_returns_AlreadyRunning_when_gate_is_held()
    {
        _gate.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(false);
        _gate.SetupGet(x => x.CurrentRunId).Returns("active-run");

        var sut = CreateSut();

        var result = sut.Trigger("5.0.0");

        result.Outcome.Should().Be(TriggerOutcome.AlreadyRunning);
        result.RunId.Should().Be("active-run");
        _launcher.Verify(x => x.Launch(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Trigger_WhenGateFree_Starts_WhenGateHeld_SecondReturnsAlreadyRunning()
    {
        var capturedRunId = "";
        var callCount = 0;
        _gate.Setup(x => x.TryAcquire(It.IsAny<string>()))
             .Returns((string id) =>
                 {
                     callCount++;
                     if (callCount == 1)
                     {
                         capturedRunId = id;
                         return true;
                     }

                     return false;
                 }
             );
        _gate.SetupGet(x => x.CurrentRunId).Returns(() => capturedRunId);

        _launcher.Setup(x => x.Launch(It.IsAny<string>())).Returns(LaunchResult(4242, "C:/uksf_exports", "5-23-9"));

        var sut = CreateSut();

        var first = sut.Trigger("5.23.9");
        var second = sut.Trigger("5.23.9");

        first.Outcome.Should().Be(TriggerOutcome.Started);
        second.Outcome.Should().Be(TriggerOutcome.AlreadyRunning);
        second.RunId.Should().Be(first.RunId);
    }

    [Fact]
    public void GetStatus_BeforeAnyTrigger_ReturnsPendingWithEmptyId()
    {
        var sut = CreateSut();
        var status = sut.GetStatus();
        status.RunId.Should().Be("");
        status.Status.Should().Be(GameDataExportStatus.Pending);
        status.StartedAt.Should().BeNull();
    }

    [Fact]
    public void GetStatus_AfterTrigger_ReturnsRunningWithRunIdAndTimestamp()
    {
        _launcher.Setup(x => x.Launch(It.IsAny<string>())).Returns(LaunchResult(4242, "C:/uksf_exports", "5-23-9")).Callback(() => Thread.Sleep(50));

        var sut = CreateSut();
        var trigger = sut.Trigger("5.23.9");
        var status = sut.GetStatus();
        status.RunId.Should().Be(trigger.RunId);
        status.Status.Should().Be(GameDataExportStatus.Running);
        status.StartedAt.Should().NotBeNull();
    }
}
