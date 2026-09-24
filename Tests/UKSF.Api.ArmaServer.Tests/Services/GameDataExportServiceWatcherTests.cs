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
                await File.WriteAllTextAsync(configFile, CompleteConfig(2048));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trigger_WhenConfigIsBelowFloorOrStopsMidClass_PersistsFailedTruncated(bool largeButUnclosed)
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
                await File.WriteAllTextAsync(outFile, largeButUnclosed ? TruncatedConfig(4096) : "tiny");
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
            File.Exists(Path.Combine(tempConfig, "config_5.23.9.cpp")).Should().BeFalse();
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
        await File.WriteAllTextAsync(outFile, TruncatedConfig(2048));
        _launcher.Setup(x => x.Launch("5.23.9")).Returns(LaunchResult(4242, outDir, "5-23-9"));
        _processUtilities.Setup(x => x.IsProcessAlive(4242)).Returns(true);
        _processUtilities.Setup(x => x.FindProcessById(4242)).Returns((System.Diagnostics.Process)null);

        var sut = CreateSutWithFastTimeouts(pollMs: 100, timeoutSeconds: 1);
        sut.Trigger("5.23.9");

        var persisted = await WaitForPersistedRecord(maxWaitMs: 10000);
        persisted.Should().NotBeNull();
        persisted.Status.Should().Be(GameDataExportStatus.FailedTimeout);
        persisted.HasConfig.Should().BeFalse();
        File.Exists(Path.Combine(tempConfig, "config_5.23.9.cpp")).Should().BeFalse();
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
}
