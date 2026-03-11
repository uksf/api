using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmDecompilerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IConfiguration> _mockConfiguration = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly SqmDecompiler _sqmDecompiler;

    public SqmDecompilerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_sqm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _mockConfiguration.Setup(c => c.GetSection("MissionPatching:DeRapDosPath").Value).Returns((string)null);

        _sqmDecompiler = new SqmDecompiler(_mockProcessCommandFactory.Object, _mockConfiguration.Object);
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
    public async Task IsBinarized_WhenExitCodeIsZero_ReturnsTrue()
    {
        var sqmPath = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllText(sqmPath, "test");

        SetupProcessCommand("exit 0");

        var result = await _sqmDecompiler.IsBinarized(sqmPath);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsBinarized_WhenExitCodeIsNonZero_ReturnsFalse()
    {
        var sqmPath = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllText(sqmPath, "test");

        SetupProcessCommand("exit 1");

        var result = await _sqmDecompiler.IsBinarized(sqmPath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Decompile_WhenOutputFileExists_MovesFileToOriginalPath()
    {
        var sqmPath = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllText(sqmPath, "binary content");

        SetupProcessCommand("echo decompiled", onExecute: () => { File.WriteAllText($"{sqmPath}.txt", "decompiled content"); });

        await _sqmDecompiler.Decompile(sqmPath);

        File.Exists(sqmPath).Should().BeTrue();
        File.ReadAllText(sqmPath).Should().Be("decompiled content");
        File.Exists($"{sqmPath}.txt").Should().BeFalse();
    }

    [Fact]
    public async Task Decompile_WhenOutputFileDoesNotExist_ThrowsInvalidOperationException()
    {
        var sqmPath = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllText(sqmPath, "binary content");

        SetupProcessCommand("echo done");

        var act = () => _sqmDecompiler.Decompile(sqmPath);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DeRapDos failed:*");
    }

    [Fact]
    public async Task Decompile_WhenStderrOutputButFileExists_Succeeds()
    {
        var sqmPath = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllText(sqmPath, "binary content");

        SetupProcessCommandWithStderr("echo version banner >&2", onExecute: () => { File.WriteAllText($"{sqmPath}.txt", "decompiled content"); });

        await _sqmDecompiler.Decompile(sqmPath);

        File.ReadAllText(sqmPath).Should().Be("decompiled content");
    }

    [Fact]
    public async Task IsBinarized_UsesProcessCommandFactory()
    {
        var sqmPath = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllText(sqmPath, "test");

        SetupProcessCommand("exit 0");

        await _sqmDecompiler.IsBinarized(sqmPath);

        _mockProcessCommandFactory.Verify(
            x => x.CreateCommand(It.IsAny<string>(), It.Is<string>(d => d == _tempDir), It.Is<string>(a => a.Contains("-p -q") && a.Contains(sqmPath))),
            Times.Once
        );
    }

    [Fact]
    public async Task Decompile_UsesProcessCommandFactory()
    {
        var sqmPath = Path.Combine(_tempDir, "mission.sqm");
        File.WriteAllText(sqmPath, "binary content");

        SetupProcessCommand("echo done", onExecute: () => { File.WriteAllText($"{sqmPath}.txt", "decompiled"); });

        await _sqmDecompiler.Decompile(sqmPath);

        _mockProcessCommandFactory.Verify(
            x => x.CreateCommand(
                It.IsAny<string>(),
                It.Is<string>(d => d == _tempDir),
                It.Is<string>(a => a.Contains("-p") && !a.Contains("-q") && a.Contains(sqmPath))
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

    private void SetupProcessCommandWithStderr(string shellCommand, Action onExecute = null)
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
