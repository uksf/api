using System.IO;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class DevRunLauncherTests
{
    private readonly Mock<ISyntheticServerLauncher> _mockSyntheticLauncher = new();
    private readonly Mock<IVariablesService> _mockVariables = new();

    public DevRunLauncherTests()
    {
        _mockVariables.Setup(x => x.GetVariable("SERVER_PATH_RELEASE"))
                      .Returns(new DomainVariableItem { Key = "SERVER_PATH_RELEASE", Item = @"C:/dev/server" });
    }

    private DevRunLauncher CreateSut() => new(_mockSyntheticLauncher.Object, _mockVariables.Object);

    [Fact]
    public void Launch_builds_dev_run_spec_and_embeds_runId_in_wrapper_sqf()
    {
        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(9001, "p", "m"));

        var modPath = Path.Combine(Path.GetTempPath(), "@cba_a3");
        Directory.CreateDirectory(modPath);
        var result = CreateSut().Launch("8c1f9d22-1234", "diag_log \"hello\";", new[] { modPath });

        result.ProcessId.Should().Be(9001);
        captured.Should().NotBeNull();
        captured.ProfileName.Should().Be("DevRun_8c1f9d22");
        captured.MissionName.Should().Be("DevRun_8c1f9d22.VR");
        captured.GamePort.Should().Be(3304);
        captured.ApiPort.Should().Be(3305);
        captured.ServerExecutablePath.Should().EndWith("arma3server_x64.exe");
        captured.Mods.Should().ContainSingle().Which.Should().Be(modPath);
        captured.FunctionFiles.Should().ContainKey("fn_runUserSqf.sqf");
        captured.FunctionFiles["fn_runUserSqf.sqf"].Should().Contain("uksf_dev_runId = \"8c1f9d22-1234\"");
        captured.FunctionFiles["fn_runUserSqf.sqf"].Should().Contain("diag_log \"\"hello\"\";"); // SQF doubled-quote escape
    }
}
