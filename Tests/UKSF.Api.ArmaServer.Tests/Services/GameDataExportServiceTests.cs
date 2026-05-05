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

public class GameDataExportServiceTests
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

    // ─── Watcher state machine tests ──────────────────────────────────────────

    [Fact]
    public async Task Trigger_WhenFileAppearsAndProcessExits_PersistsSuccessAndCopiesFile()
    {
        var outDir = NewTempDir();
        var tempConfig = NewTempDir();
        var tempSettings = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", tempSettings);

        var configFile = Path.Combine(outDir, "config_2.20_uksf-5-23-9.cpp");
        var cbaSettingsFile = Path.Combine(outDir, "cba_settings_2.20_uksf-5-23-9.sqf");
        var cbaReferenceFile = Path.Combine(outDir, "cba_settings_reference_2.20_uksf-5-23-9.json");

        _launcher.Setup(x => x.Launch("5.23.9")).Returns(LaunchResult(4242, outDir, "5-23-9"));

        var processAlive = true;
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(() => processAlive);

        _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                await File.WriteAllTextAsync(configFile, new string('x', 2048));
                await File.WriteAllTextAsync(cbaSettingsFile, "force CBA_test = 1;\n");
                await File.WriteAllTextAsync(cbaReferenceFile, """{"settings":[1,2,3]}""");
                processAlive = false;
            }
        );

        var sut = CreateFastSut();
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(GameDataExportStatus.Success);
            persisted.GameVersion.Should().Be("2.20");
            persisted.ModpackVersion.Should().Be("5.23.9");
            persisted.HasConfig.Should().BeTrue();
            persisted.HasCbaSettings.Should().BeTrue();
            persisted.HasCbaSettingsReference.Should().BeTrue();
            File.Exists(Path.Combine(tempConfig, "config_5.23.9.cpp")).Should().BeTrue();
            File.Exists(Path.Combine(tempSettings, "cba_settings_5.23.9.sqf")).Should().BeTrue();
            File.Exists(Path.Combine(tempSettings, "cba_settings_reference_5.23.9.json")).Should().BeTrue();
            sut.GetStatus().Status.Should().Be(GameDataExportStatus.Success);
        }
        finally
        {
            try
            {
                Directory.Delete(outDir, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempConfig, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempSettings, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Trigger_WhenProcessExitsWithNoFile_PersistsFailedNoOutput()
    {
        var outDir = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", NewTempDir());
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", NewTempDir());

        _launcher.Setup(x => x.Launch("5.23.9")).Returns(LaunchResult(4242, outDir, "5-23-9"));

        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(false);

        var sut = CreateFastSut();
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(GameDataExportStatus.FailedNoOutput);
            persisted.ModpackVersion.Should().Be("5.23.9");
            sut.GetStatus().Status.Should().Be(GameDataExportStatus.FailedNoOutput);
        }
        finally
        {
            try
            {
                Directory.Delete(outDir, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Trigger_WhenFileIsBelowSanityFloor_PersistsFailedTruncated()
    {
        var outDir = NewTempDir();
        var tempConfig = NewTempDir();
        var tempSettings = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", tempSettings);

        var outFile = Path.Combine(outDir, "config_2.20_uksf-5-23-9.cpp");

        _launcher.Setup(x => x.Launch("5.23.9")).Returns(LaunchResult(4242, outDir, "5-23-9"));

        var processAlive = true;
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(() => processAlive);

        _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                await File.WriteAllTextAsync(outFile, "tiny");
                processAlive = false;
            }
        );

        var sut = CreateFastSut();
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(GameDataExportStatus.FailedTruncated);
            persisted.ModpackVersion.Should().Be("5.23.9");
            persisted.HasConfig.Should().BeFalse();
            sut.GetStatus().Status.Should().Be(GameDataExportStatus.FailedTruncated);
        }
        finally
        {
            try
            {
                Directory.Delete(outDir, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempConfig, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempSettings, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Trigger_WhenWallClockTimeoutExpires_PersistsFailedTimeout()
    {
        var outDir = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", NewTempDir());
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", NewTempDir());

        _launcher.Setup(x => x.Launch("5.23.9")).Returns(LaunchResult(4242, outDir, "5-23-9"));

        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(true);
        _processUtilities.Setup(x => x.FindProcessById(4242)).Returns((System.Diagnostics.Process)null);

        var sut = CreateSutWithFastTimeouts(pollMs: 100, timeoutSeconds: 1);
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord(maxWaitMs: 10000);

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(GameDataExportStatus.FailedTimeout);
            persisted.ModpackVersion.Should().Be("5.23.9");
            sut.GetStatus().Status.Should().Be(GameDataExportStatus.FailedTimeout);
        }
        finally
        {
            try
            {
                Directory.Delete(outDir, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Trigger_WhenProcessNeverExits_HitsWallClockTimeoutAndKillsProcess()
    {
        var outDir = NewTempDir();
        var tempConfig = NewTempDir();
        var tempSettings = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", tempSettings);

        var outFile = Path.Combine(outDir, "config_2.20_uksf-5-23-9.cpp");
        await File.WriteAllTextAsync(outFile, new string('x', 2048));
        _launcher.Setup(x => x.Launch("5.23.9")).Returns(LaunchResult(4242, outDir, "5-23-9"));
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(true);
        _processUtilities.Setup(x => x.FindProcessById(4242)).Returns((System.Diagnostics.Process)null);

        var sut = CreateSutWithFastTimeouts(pollMs: 100, timeoutSeconds: 1);
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord(maxWaitMs: 10000);
        persisted.Should().NotBeNull();
        persisted.Status.Should().Be(GameDataExportStatus.FailedTimeout);
        persisted.HasConfig.Should().BeTrue();
        File.Exists(Path.Combine(tempConfig, "config_5.23.9.cpp")).Should().BeTrue();
        _processUtilities.Verify(x => x.FindProcessById(4242), Times.Once);

        try
        {
            Directory.Delete(outDir, true);
        }
        catch { }

        try
        {
            Directory.Delete(tempConfig, true);
        }
        catch { }

        try
        {
            Directory.Delete(tempSettings, true);
        }
        catch { }
    }

    // ─── New multi-file FinishAsync tests (Phase 4 plan) ──────────────────────

    [Fact]
    public async Task FinishAsync_All_Three_Files_Present_Stores_All_With_Status_Success()
    {
        var tempSource = NewTempDir();
        var tempConfig = NewTempDir();
        var tempSettings = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", tempSettings);

        File.WriteAllText(Path.Combine(tempSource, "config_2.20_uksf-5-23-9.cpp"), new string('x', 4096));
        File.WriteAllText(Path.Combine(tempSource, "cba_settings_2.20_uksf-5-23-9.sqf"), "force CBA_test = 1;\n");
        File.WriteAllText(Path.Combine(tempSource, "cba_settings_reference_2.20_uksf-5-23-9.json"), """{"settings":[1,2,3]}""");

        var launch = new GameDataExportLaunchResult(
            ProcessId: 999,
            ExpectedOutputDirectory: tempSource,
            ConfigGlob: "config_*_uksf-5-23-9.cpp",
            CbaSettingsGlob: "cba_settings_*_uksf-5-23-9.sqf",
            CbaSettingsReferenceGlob: "cba_settings_reference_*_uksf-5-23-9.json"
        );

        _processUtilities.Setup(x => x.IsProcessAlive(999)).Returns(false);
        _launcher.Setup(x => x.Launch("5.23.9")).Returns(launch);

        var sut = CreateSutWithFastTimeouts(pollMs: 10, timeoutSeconds: 5);
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(GameDataExportStatus.Success);
            persisted.HasConfig.Should().BeTrue();
            persisted.HasCbaSettings.Should().BeTrue();
            persisted.HasCbaSettingsReference.Should().BeTrue();
            persisted.GameVersion.Should().Be("2.20");

            File.Exists(Path.Combine(tempConfig, "config_5.23.9.cpp")).Should().BeTrue();
            File.Exists(Path.Combine(tempSettings, "cba_settings_5.23.9.sqf")).Should().BeTrue();
            File.Exists(Path.Combine(tempSettings, "cba_settings_reference_5.23.9.json")).Should().BeTrue();
        }
        finally
        {
            try
            {
                Directory.Delete(tempSource, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempConfig, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempSettings, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task FinishAsync_Only_Config_Present_Stores_PartialSuccess()
    {
        var tempSource = NewTempDir();
        var tempConfig = NewTempDir();
        var tempSettings = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", tempSettings);

        File.WriteAllText(Path.Combine(tempSource, "config_2.20_uksf-5-23-9.cpp"), new string('x', 4096));

        var launch = new GameDataExportLaunchResult(
            999,
            tempSource,
            "config_*_uksf-5-23-9.cpp",
            "cba_settings_*_uksf-5-23-9.sqf",
            "cba_settings_reference_*_uksf-5-23-9.json"
        );

        _processUtilities.Setup(x => x.IsProcessAlive(999)).Returns(false);
        _launcher.Setup(x => x.Launch("5.23.9")).Returns(launch);

        var sut = CreateSutWithFastTimeouts(pollMs: 10, timeoutSeconds: 5);
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(GameDataExportStatus.PartialSuccess);
            persisted.HasConfig.Should().BeTrue();
            persisted.HasCbaSettings.Should().BeFalse();
            persisted.HasCbaSettingsReference.Should().BeFalse();
        }
        finally
        {
            try
            {
                Directory.Delete(tempSource, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempConfig, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempSettings, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task FinishAsync_Reference_Json_Unparseable_Marks_Truncated()
    {
        var tempSource = NewTempDir();
        var tempConfig = NewTempDir();
        var tempSettings = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", tempSettings);

        File.WriteAllText(Path.Combine(tempSource, "config_2.20_uksf-5-23-9.cpp"), new string('x', 4096));
        File.WriteAllText(Path.Combine(tempSource, "cba_settings_2.20_uksf-5-23-9.sqf"), "force CBA_test = 1;\n");
        File.WriteAllText(Path.Combine(tempSource, "cba_settings_reference_2.20_uksf-5-23-9.json"), "{not valid json but at least 16 bytes long");

        var launch = new GameDataExportLaunchResult(
            999,
            tempSource,
            "config_*_uksf-5-23-9.cpp",
            "cba_settings_*_uksf-5-23-9.sqf",
            "cba_settings_reference_*_uksf-5-23-9.json"
        );

        _processUtilities.Setup(x => x.IsProcessAlive(999)).Returns(false);
        _launcher.Setup(x => x.Launch("5.23.9")).Returns(launch);

        var sut = CreateSutWithFastTimeouts(pollMs: 10, timeoutSeconds: 5);
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(GameDataExportStatus.FailedTruncated);
            persisted.HasConfig.Should().BeTrue();
            persisted.HasCbaSettings.Should().BeTrue();
            persisted.HasCbaSettingsReference.Should().BeFalse();

            File.Exists(Path.Combine(tempSettings, "cba_settings_reference_5.23.9.json")).Should().BeFalse();
        }
        finally
        {
            try
            {
                Directory.Delete(tempSource, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempConfig, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempSettings, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task FinishAsync_No_Files_Present_Stores_FailedNoOutput()
    {
        var tempSource = NewTempDir();
        var tempConfig = NewTempDir();
        var tempSettings = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", tempSettings);

        var launch = new GameDataExportLaunchResult(
            999,
            tempSource,
            "config_*_uksf-5-23-9.cpp",
            "cba_settings_*_uksf-5-23-9.sqf",
            "cba_settings_reference_*_uksf-5-23-9.json"
        );

        _processUtilities.Setup(x => x.IsProcessAlive(999)).Returns(false);
        _launcher.Setup(x => x.Launch("5.23.9")).Returns(launch);

        var sut = CreateSutWithFastTimeouts(pollMs: 10, timeoutSeconds: 5);
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(GameDataExportStatus.FailedNoOutput);
            persisted.HasConfig.Should().BeFalse();
            persisted.HasCbaSettings.Should().BeFalse();
            persisted.HasCbaSettingsReference.Should().BeFalse();
        }
        finally
        {
            try
            {
                Directory.Delete(tempSource, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempConfig, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempSettings, true);
            }
            catch { }
        }
    }
}
