using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.ScheduledActions;

public class ConfigExportRecoveryStartupTests
{
    private readonly Mock<IGameServerHelpers> _helpers = new();
    private readonly Mock<IProcessUtilities> _processUtilities = new();
    private readonly Mock<IGameConfigExportsContext> _context = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IUksfLogger> _logger = new();

    public ConfigExportRecoveryStartupTests()
    {
        _context.Setup(x => x.Add(It.IsAny<DomainGameConfigExport>())).Returns(Task.CompletedTask);
    }

    private static DomainVariableItem CreateVariable(string key, object item) => new() { Key = key, Item = item };

    private void SetupVariable(string key, string value)
    {
        _variablesService.Setup(x => x.GetVariable(key)).Returns(CreateVariable(key, value));
    }

    private ConfigExportRecoveryStartup CreateSut() =>
        new(_helpers.Object, _processUtilities.Object, _context.Object, _variablesService.Object, _logger.Object);

    [Fact]
    public async Task StartAsync_LogsKeyOfMissingVariable()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_CONFIG_EXPORT")).Returns(new DomainVariableItem { Key = "SERVER_PATH_CONFIG_EXPORT" });
        _processUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(Array.Empty<ProcessCommandLineInfo>());

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _logger.Verify(x => x.LogError(It.Is<Exception>(ex => ex.Message.Contains("SERVER_PATH_CONFIG_EXPORT"))), Times.Once);
    }

    [Fact]
    public async Task StartAsync_KillsOrphanConfigExportProcess()
    {
        var orphan = new ProcessCommandLineInfo(9999, "\"arma3server_x64.exe\" -profiles=C:/p/ConfigExport -port=3302");
        _processUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(new[] { orphan });
        _helpers.Setup(x => x.IsConfigExportProcess(It.IsAny<string>())).Returns(true);
        _processUtilities.Setup(x => x.FindProcessById(9999)).Returns((System.Diagnostics.Process)null);

        // Salvage path: no export dir configured so it won't scan
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid()));

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _processUtilities.Verify(x => x.FindProcessById(9999), Times.Once);
    }

    [Fact]
    public async Task StartAsync_SalvagesUnrecordedCppFile()
    {
        var tempStorage = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-recover-" + Guid.NewGuid());
        Directory.CreateDirectory(tempStorage);
        var file = Path.Combine(tempStorage, "config_5.23.9.cpp");
        await File.WriteAllTextAsync(file, "...");
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempStorage);

        _processUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(Array.Empty<ProcessCommandLineInfo>());
        _context.Setup(x => x.Get(It.IsAny<Func<DomainGameConfigExport, bool>>())).Returns(new List<DomainGameConfigExport>());

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _context.Verify(
            x => x.Add(It.Is<DomainGameConfigExport>(r => r.ModpackVersion == "5.23.9" && r.Status == ConfigExportStatus.Success && r.FilePath == file)),
            Times.Once
        );

        try
        {
            Directory.Delete(tempStorage, true);
        }
        catch { }
    }

    [Fact]
    public async Task StartAsync_DoesNotSalvageOldFiles()
    {
        var tempStorage = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-old-" + Guid.NewGuid());
        Directory.CreateDirectory(tempStorage);
        var file = Path.Combine(tempStorage, "config_4.0.0.cpp");
        await File.WriteAllTextAsync(file, "...");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-2));
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempStorage);

        _processUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(Array.Empty<ProcessCommandLineInfo>());
        _context.Setup(x => x.Get(It.IsAny<Func<DomainGameConfigExport, bool>>())).Returns(new List<DomainGameConfigExport>());

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _context.Verify(x => x.Add(It.IsAny<DomainGameConfigExport>()), Times.Never);

        try
        {
            Directory.Delete(tempStorage, true);
        }
        catch { }
    }

    [Fact]
    public async Task StartAsync_DoesNotSalvageAlreadyRecordedFiles()
    {
        var tempStorage = Path.Combine(Path.GetTempPath(), "uksf-cfgexport-known-" + Guid.NewGuid());
        Directory.CreateDirectory(tempStorage);
        var file = Path.Combine(tempStorage, "config_5.23.9.cpp");
        await File.WriteAllTextAsync(file, "...");
        SetupVariable("SERVER_PATH_CONFIG_EXPORT", tempStorage);

        _processUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(Array.Empty<ProcessCommandLineInfo>());
        _context.Setup(x => x.Get(It.IsAny<Func<DomainGameConfigExport, bool>>()))
                .Returns(new List<DomainGameConfigExport> { new() { ModpackVersion = "5.23.9", Status = ConfigExportStatus.Success } });

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _context.Verify(x => x.Add(It.IsAny<DomainGameConfigExport>()), Times.Never);

        try
        {
            Directory.Delete(tempStorage, true);
        }
        catch { }
    }
}
