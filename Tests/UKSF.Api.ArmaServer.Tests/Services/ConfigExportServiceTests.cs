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

public class ConfigExportServiceTests
{
    private readonly Mock<IConfigExportProcessLauncher> _launcher = new();
    private readonly Mock<IGameConfigExportsContext> _context = new();
    private readonly Mock<IProcessUtilities> _processUtilities = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IUksfLogger> _logger = new();

    private readonly List<DomainGameConfigExport> _persistedRecords = new();

    public ConfigExportServiceTests()
    {
        _context.Setup(x => x.Add(It.IsAny<DomainGameConfigExport>()))
                .Callback<DomainGameConfigExport>(record => _persistedRecords.Add(record))
                .Returns(Task.CompletedTask);
    }

    private ConfigExportService CreateSut()
    {
        return new ConfigExportService(_launcher.Object, _context.Object, _processUtilities.Object, _variablesService.Object, _logger.Object);
    }

    // Fast-polling SUT: 100 ms poll, 5 s wall-clock timeout — keeps watcher tests well under 10 s.
    private ConfigExportService CreateFastSut()
    {
        return new ConfigExportService(
            _launcher.Object,
            _context.Object,
            _processUtilities.Object,
            _variablesService.Object,
            _logger.Object,
            pollMs: 100,
            timeoutSeconds: 5
        );
    }

    // Parameterised SUT: explicit poll and timeout intervals.
    private ConfigExportService CreateSutWithFastTimeouts(int pollMs, int timeoutSeconds)
    {
        return new ConfigExportService(
            _launcher.Object,
            _context.Object,
            _processUtilities.Object,
            _variablesService.Object,
            _logger.Object,
            pollMs: pollMs,
            timeoutSeconds: timeoutSeconds
        );
    }

    private static DomainVariableItem CreateVariable(string key, object item) => new() { Key = key, Item = item };

    private void SetupVariable(string key, string value)
    {
        _variablesService.Setup(x => x.GetVariable(key)).Returns(CreateVariable(key, value));
    }

    private async Task<DomainGameConfigExport> WaitForPersistedRecord(int maxWaitMs = 5000)
    {
        for (var i = 0; i < maxWaitMs / 50; i++)
        {
            await Task.Delay(50);
            if (_persistedRecords.Count > 0) return _persistedRecords.Last();
        }

        return null;
    }

    // ─── Existing gate / status tests ────────────────────────────────────────

    [Fact]
    public void Trigger_FirstCallStarts_SecondReturnsAlreadyRunning()
    {
        _launcher.Setup(x => x.Launch(It.IsAny<string>()))
                 .Returns(new ConfigExportLaunchResult(4242, "C:/uksf_config_export", "config_*_uksf-5.23.9.cpp"))
                 .Callback(() => Thread.Sleep(200)); // simulate launch taking measurable time

        var sut = CreateSut();

        var first = sut.Trigger("5.23.9");
        var second = sut.Trigger("5.23.9"); // immediate, while first still running

        first.Outcome.Should().Be(TriggerOutcome.Started);
        second.Outcome.Should().Be(TriggerOutcome.AlreadyRunning);
        first.RunId.Should().Be(second.RunId);
    }

    [Fact]
    public void GetStatus_BeforeAnyTrigger_ReturnsPendingWithEmptyId()
    {
        var sut = CreateSut();
        var status = sut.GetStatus();
        status.RunId.Should().Be("");
        status.Status.Should().Be(ConfigExportStatus.Pending);
        status.StartedAt.Should().BeNull();
    }

    [Fact]
    public void GetStatus_AfterTrigger_ReturnsRunningWithRunIdAndTimestamp()
    {
        _launcher.Setup(x => x.Launch(It.IsAny<string>()))
                 .Returns(new ConfigExportLaunchResult(4242, "C:/uksf_config_export", "config_*_uksf-5.23.9.cpp"))
                 .Callback(() => Thread.Sleep(50));

        var sut = CreateSut();
        var trigger = sut.Trigger("5.23.9");
        var status = sut.GetStatus();
        status.RunId.Should().Be(trigger.RunId);
        status.Status.Should().Be(ConfigExportStatus.Running);
        status.StartedAt.Should().NotBeNull();
    }

    // ─── Watcher state machine tests ──────────────────────────────────────────

    [Fact]
    public async Task Trigger_WhenFileAppearsAndProcessExits_PersistsSuccessAndCopiesFile()
    {
        var tempArmaRoot = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-" + Guid.NewGuid());
        var tempStorage = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-store-" + Guid.NewGuid());
        var outDir = Path.Combine(tempArmaRoot, "uksf_config_export");
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(tempStorage);
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempStorage);

        var outFile = Path.Combine(outDir, "config_2.20_uksf-5-23.cpp");

        _launcher.Setup(x => x.Launch("5.23.9")).Returns(new ConfigExportLaunchResult(4242, outDir, "config_*_uksf-5-23.cpp"));

        var processAlive = true;
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(() => processAlive);

        _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                await File.WriteAllTextAsync(outFile, new string('x', 2048));
                processAlive = false;
            }
        );

        var sut = CreateFastSut();
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(ConfigExportStatus.Success);
            persisted.GameVersion.Should().Be("2.20");
            persisted.ModpackVersion.Should().Be("5.23.9");
            File.Exists(Path.Combine(tempStorage, "config_5.23.9.cpp")).Should().BeTrue();
            sut.GetStatus().Status.Should().Be(ConfigExportStatus.Success);
        }
        finally
        {
            try
            {
                Directory.Delete(tempArmaRoot, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempStorage, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Trigger_WhenProcessExitsWithNoFile_PersistsFailedNoOutput()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-nofile-" + Guid.NewGuid());
        Directory.CreateDirectory(outDir);

        _launcher.Setup(x => x.Launch("5.23.9")).Returns(new ConfigExportLaunchResult(4242, outDir, "config_*_uksf-5.23.9.cpp"));

        // Process exits immediately; no file ever appears.
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(false);

        var sut = CreateFastSut();
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord();

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(ConfigExportStatus.FailedNoOutput);
            persisted.ModpackVersion.Should().Be("5.23.9");
            sut.GetStatus().Status.Should().Be(ConfigExportStatus.FailedNoOutput);
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
        var tempArmaRoot = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-trunc-" + Guid.NewGuid());
        var tempStorage = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-trunc-store-" + Guid.NewGuid());
        var outDir = Path.Combine(tempArmaRoot, "uksf_config_export");
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(tempStorage);
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempStorage);

        var outFile = Path.Combine(outDir, "config_2.20_uksf-5-23.cpp");

        _launcher.Setup(x => x.Launch("5.23.9")).Returns(new ConfigExportLaunchResult(4242, outDir, "config_*_uksf-5-23.cpp"));

        var processAlive = true;
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(() => processAlive);

        _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                // Write a tiny file (10 bytes) — well below the 1 KB sanity floor.
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
            persisted.Status.Should().Be(ConfigExportStatus.FailedTruncated);
            persisted.ModpackVersion.Should().Be("5.23.9");
            // No file should have been copied for a truncated result.
            persisted.FilePath.Should().BeNull();
            sut.GetStatus().Status.Should().Be(ConfigExportStatus.FailedTruncated);
        }
        finally
        {
            try
            {
                Directory.Delete(tempArmaRoot, true);
            }
            catch { }

            try
            {
                Directory.Delete(tempStorage, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Trigger_WhenWallClockTimeoutExpires_PersistsFailedTimeout()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-timeout-" + Guid.NewGuid());
        Directory.CreateDirectory(outDir);

        _launcher.Setup(x => x.Launch("5.23.9")).Returns(new ConfigExportLaunchResult(4242, outDir, "config_*_uksf-5.23.9.cpp"));

        // Process stays alive and no file appears — forces timeout.
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(true);
        _processUtilities.Setup(x => x.FindProcessById(4242)).Returns((System.Diagnostics.Process)null);

        // Use a very short timeout: 1 second, 100 ms poll.
        var sut = new ConfigExportService(
            _launcher.Object,
            _context.Object,
            _processUtilities.Object,
            _variablesService.Object,
            _logger.Object,
            pollMs: 100,
            timeoutSeconds: 1
        );
        sut.Trigger("5.23.9");

        // Wait up to 10 s — timeout fires after ~1 s + one poll cycle.
        var persisted = await WaitForPersistedRecord(maxWaitMs: 10000);

        try
        {
            persisted.Should().NotBeNull();
            persisted.Status.Should().Be(ConfigExportStatus.FailedTimeout);
            persisted.ModpackVersion.Should().Be("5.23.9");
            sut.GetStatus().Status.Should().Be(ConfigExportStatus.FailedTimeout);
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
        var tempArmaRoot = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-timeout-" + Guid.NewGuid());
        var tempStorage = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-timeout-store-" + Guid.NewGuid());
        var outDir = Path.Combine(tempArmaRoot, "uksf_config_export");
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(tempStorage);
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempStorage);

        // File present from the start, but process never exits — the watcher must wait for
        // process-exit (the natural completion signal) and only kill on wall-clock timeout.
        var outFile = Path.Combine(outDir, "config_2.20_uksf-5-23-9.cpp");
        await File.WriteAllTextAsync(outFile, new string('x', 2048));
        _launcher.Setup(x => x.Launch("5.23.9")).Returns(new ConfigExportLaunchResult(4242, outDir, "config_*_uksf-5-23-9.cpp"));
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(true);
        _processUtilities.Setup(x => x.FindProcessById(4242)).Returns((System.Diagnostics.Process)null);

        var sut = CreateSutWithFastTimeouts(pollMs: 100, timeoutSeconds: 1);
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord(maxWaitMs: 10000);
        persisted.Should().NotBeNull();
        persisted.Status.Should().Be(ConfigExportStatus.FailedTimeout);
        persisted.FilePath.Should().NotBeNull(); // file salvaged from disk despite timeout
        File.Exists(Path.Combine(tempStorage, "config_5.23.9.cpp")).Should().BeTrue();
        _processUtilities.Verify(x => x.FindProcessById(4242), Times.Once);

        try
        {
            Directory.Delete(tempArmaRoot, true);
        }
        catch { }

        try
        {
            Directory.Delete(tempStorage, true);
        }
        catch { }
    }
}
