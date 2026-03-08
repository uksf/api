using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UKSF.Api.ArmaMissions.Exceptions;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class PboToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IConfiguration> _mockConfiguration = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly PboTools _pboTools;

    public PboToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_pbo_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _mockConfiguration.Setup(c => c.GetSection(It.IsAny<string>()).Value).Returns((string)null);

        _pboTools = new PboTools(_mockProcessCommandFactory.Object, _mockConfiguration.Object);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task ExtractPbo_WhenFolderCreated_Succeeds()
    {
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        File.WriteAllText(pboPath, "fake pbo");

        SetupProcessCommand("echo extracting", onExecute: () => { Directory.CreateDirectory(Path.Combine(_tempDir, "testmission")); });

        await _pboTools.ExtractPbo(pboPath, _tempDir);

        Directory.Exists(Path.Combine(_tempDir, "testmission")).Should().BeTrue();
    }

    [Fact]
    public async Task ExtractPbo_WhenFolderNotCreated_ThrowsDirectoryNotFoundException()
    {
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        File.WriteAllText(pboPath, "fake pbo");

        SetupProcessCommand("echo done");

        var act = () => _pboTools.ExtractPbo(pboPath, _tempDir);

        await act.Should().ThrowAsync<DirectoryNotFoundException>().WithMessage("Could not find unpacked pbo");
    }

    [Fact]
    public async Task ExtractPbo_WhenExistingFolderExists_DeletesItFirst()
    {
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        File.WriteAllText(pboPath, "fake pbo");

        var existingFolder = Path.Combine(_tempDir, "testmission");
        Directory.CreateDirectory(existingFolder);
        File.WriteAllText(Path.Combine(existingFolder, "old_file.txt"), "old content");

        SetupProcessCommand(
            "echo extracting",
            onExecute: () =>
            {
                Directory.CreateDirectory(existingFolder);
                File.WriteAllText(Path.Combine(existingFolder, "new_file.txt"), "new content");
            }
        );

        await _pboTools.ExtractPbo(pboPath, _tempDir);

        File.Exists(Path.Combine(existingFolder, "old_file.txt")).Should().BeFalse();
        File.Exists(Path.Combine(existingFolder, "new_file.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ExtractPbo_WhenProcessReportsError_ThrowsPboOperationException()
    {
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        File.WriteAllText(pboPath, "fake pbo");

        SetupProcessCommandWithStderr("echo error message >&2");

        var act = () => _pboTools.ExtractPbo(pboPath, _tempDir);

        await act.Should().ThrowAsync<PboOperationException>();
    }

    [Fact]
    public async Task MakePbo_WhenPboFileCreated_Succeeds()
    {
        var folderPath = Path.Combine(_tempDir, "testmission");
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        Directory.CreateDirectory(folderPath);

        SetupProcessCommand("echo packing", onExecute: () => { File.WriteAllText(pboPath, "packed pbo"); });

        await _pboTools.MakePbo(folderPath, pboPath, _tempDir);

        File.Exists(pboPath).Should().BeTrue();
    }

    [Fact]
    public async Task MakePbo_WhenPboFileNotCreated_ThrowsPboOperationException()
    {
        var folderPath = Path.Combine(_tempDir, "testmission");
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        Directory.CreateDirectory(folderPath);

        SetupProcessCommand("echo some error output");

        var act = () => _pboTools.MakePbo(folderPath, pboPath, _tempDir);

        await act.Should().ThrowAsync<PboOperationException>();
    }

    [Fact]
    public async Task MakePbo_UsesCorrectArguments()
    {
        var folderPath = Path.Combine(_tempDir, "testmission");
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        Directory.CreateDirectory(folderPath);

        SetupProcessCommand("echo done", onExecute: () => { File.WriteAllText(pboPath, "packed"); });

        await _pboTools.MakePbo(folderPath, pboPath, _tempDir);

        _mockProcessCommandFactory.Verify(
            x => x.CreateCommand(
                It.IsAny<string>(),
                It.Is<string>(d => d == _tempDir),
                It.Is<string>(a => a.Contains("-Z -BD -P -X=") && a.Contains(folderPath))
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SimplePackPbo_WhenPboFileCreated_Succeeds()
    {
        var folderPath = Path.Combine(_tempDir, "testmission");
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        Directory.CreateDirectory(folderPath);

        SetupProcessCommand("echo packing", onExecute: () => { File.WriteAllText(pboPath, "packed pbo"); });

        await _pboTools.SimplePackPbo(folderPath, pboPath, _tempDir);

        File.Exists(pboPath).Should().BeTrue();
    }

    [Fact]
    public async Task SimplePackPbo_WhenPboFileCreated_DeletesBackupFile()
    {
        var folderPath = Path.Combine(_tempDir, "testmission");
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        Directory.CreateDirectory(folderPath);

        SetupProcessCommand(
            "echo packing",
            onExecute: () =>
            {
                File.WriteAllText(pboPath, "packed pbo");
                File.WriteAllText($"{pboPath}.bak", "backup");
            }
        );

        await _pboTools.SimplePackPbo(folderPath, pboPath, _tempDir);

        File.Exists($"{pboPath}.bak").Should().BeFalse();
    }

    [Fact]
    public async Task SimplePackPbo_WhenPboFileNotCreated_ThrowsPboOperationException()
    {
        var folderPath = Path.Combine(_tempDir, "testmission");
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        Directory.CreateDirectory(folderPath);

        SetupProcessCommand("echo some error output");

        var act = () => _pboTools.SimplePackPbo(folderPath, pboPath, _tempDir);

        await act.Should().ThrowAsync<PboOperationException>();
    }

    [Fact]
    public async Task SimplePackPbo_UsesCorrectArguments()
    {
        var folderPath = Path.Combine(_tempDir, "testmission");
        var pboPath = Path.Combine(_tempDir, "testmission.pbo");
        Directory.CreateDirectory(folderPath);

        SetupProcessCommand("echo done", onExecute: () => { File.WriteAllText(pboPath, "packed"); });

        await _pboTools.SimplePackPbo(folderPath, pboPath, _tempDir);

        _mockProcessCommandFactory.Verify(
            x => x.CreateCommand(
                It.IsAny<string>(),
                It.Is<string>(d => d == _tempDir),
                It.Is<string>(a => a.Contains("-pack") && a.Contains(folderPath) && a.Contains(pboPath))
            ),
            Times.Once
        );
    }

    private void SetupProcessCommand(string shellCommand, Action onExecute = null)
    {
        GetPlatformCommand(out var executable, out var args, shellCommand);

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns((string _, string workingDir, string _) =>
                                      {
                                          onExecute?.Invoke();
                                          return new ProcessCommand(_mockLogger.Object, executable, workingDir, args).WithTimeout(TimeSpan.FromSeconds(5));
                                      }
                                  );
    }

    private void SetupProcessCommandWithStderr(string shellCommand)
    {
        GetPlatformCommand(out var executable, out var args, shellCommand);

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns((string _, string workingDir, string _) =>
                                               new ProcessCommand(_mockLogger.Object, executable, workingDir, args).WithTimeout(TimeSpan.FromSeconds(5))
                                  );
    }

    private static void GetPlatformCommand(out string executable, out string args, string command)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = $"/c \"{command}\"";
        }
        else
        {
            executable = "sh";
            args = $"-c \"{command}\"";
        }
    }
}
