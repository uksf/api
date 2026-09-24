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
    // ─── New multi-file FinishAsync tests (Phase 4 plan) ──────────────────────

    [Fact]
    public async Task FinishAsync_All_Three_Files_Present_Stores_All_With_Status_Success()
    {
        var tempSource = NewTempDir();
        var tempConfig = NewTempDir();
        var tempSettings = NewTempDir();
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempConfig);
        SetupVariable("SERVER_PATH_SETTINGS_EXPORT", tempSettings);

        File.WriteAllText(Path.Combine(tempSource, "config_2.20_uksf-5-23-9.cpp"), CompleteConfig(4096));
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

        File.WriteAllText(Path.Combine(tempSource, "config_2.20_uksf-5-23-9.cpp"), CompleteConfig(4096));

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

        File.WriteAllText(Path.Combine(tempSource, "config_2.20_uksf-5-23-9.cpp"), CompleteConfig(4096));
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
