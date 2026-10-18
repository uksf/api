using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class DevRunHeadlessClientTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "devrun-hc-" + Guid.NewGuid());
    private readonly Mock<IProcessUtilities> _processUtilities = new();

    public DevRunHeadlessClientTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }

    private SyntheticLaunchSpec CaptureDevRunSpec(int headlessClients)
    {
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("SERVER_PATH_DEV")).Returns(new DomainVariableItem { Key = "SERVER_PATH_DEV", Item = "C:/dev/server" });
        variables.Setup(x => x.GetVariable("MODPACK_REPO_PATH")).Returns(new DomainVariableItem { Key = "MODPACK_REPO_PATH", Item = _tempRoot });
        var synthetic = new Mock<ISyntheticServerLauncher>();
        SyntheticLaunchSpec captured = null;
        synthetic.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                 .Callback<SyntheticLaunchSpec>(spec => captured = spec)
                 .Returns(new SyntheticLaunchResult(1, "p", "m"));

        new DevRunLauncher(synthetic.Object, variables.Object).Launch("8c1f9d22-1234", "diag_log 1;", [], null, headlessClients);
        return captured;
    }

    [Fact]
    public void Dev_run_mission_has_one_headless_client_slot_per_requested_client()
    {
        var spec = CaptureDevRunSpec(2);

        spec.HeadlessClients.Should().Be(2);
        Regex.Matches(spec.MissionSqm, "type=\"HeadlessClient_F\";").Count.Should().Be(2);
        spec.MissionSqm.Should().Contain("name=\"HC1\";").And.Contain("name=\"HC2\";").And.Contain("items=3;");
        spec.ServerConfig.Should().Contain("headlessClients[] = {\"127.0.0.1\"};").And.Contain("localClient[] = {\"127.0.0.1\"};");
        spec.FunctionFiles["fn_runUserSqf.sqf"].Should().Contain($"[\"{DevRunLauncher.MissionRunningLogLine}\"] call uksf_dev_fnc_logLine;");
    }

    [Fact]
    public void Dev_run_mission_without_headless_clients_keeps_only_the_player_slot()
    {
        var spec = CaptureDevRunSpec(0);

        spec.HeadlessClients.Should().Be(0);
        spec.MissionSqm.Should().NotContain("HeadlessClient_F").And.NotContain("HEADLESS_CLIENT_SLOTS").And.NotContain("ENTITY_COUNT");
        spec.MissionSqm.Should().Contain("items=1;\n");
        spec.FunctionFiles["fn_runUserSqf.sqf"].Should().NotContain(DevRunLauncher.MissionRunningLogLine);
    }

    [Fact]
    public void Synthetic_launcher_starts_only_the_server_and_returns_headless_clients_that_dial_the_server_port()
    {
        _processUtilities.Setup(x => x.LaunchManagedProcess(It.IsAny<string>(), It.IsAny<string>())).Returns(100);
        var profiles = Path.Combine(_tempRoot, "profiles");
        var launcher = new SyntheticServerLauncher(_processUtilities.Object, profiles, _tempRoot, _tempRoot);
        var spec = new SyntheticLaunchSpec(
            "DevRun_8c1f9d22",
            "DevRun_8c1f9d22.cfg",
            "DevRun_8c1f9d22.VR",
            "C:/dev/arma3server_x64.exe",
            3304,
            3305,
            [],
            "hostname=\"x\";",
            "version=54;",
            "",
            new Dictionary<string, string>(),
            HeadlessClients: 2
        );

        var result = launcher.Launch(spec);

        result.ProcessId.Should().Be(100);
        _processUtilities.Verify(x => x.LaunchManagedProcess(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        result.PendingHeadlessClients.Should().HaveCount(2).And.OnlyContain(x => x.Executable == "C:/dev/arma3server_x64.exe");
        var helpers = new GameServerHelpers(
            new Mock<IVariablesService>().Object,
            _processUtilities.Object,
            new Mock<IUksfLogger>().Object,
            new ConfigurationBuilder().Build()
        );
        foreach (var (args, index) in result.PendingHeadlessClients.Select((x, i) => (x.Arguments, i + 1)))
        {
            args.Should()
                .Contain("-client -connect=127.0.0.1:3304 ")
                .And.NotContain("-port=")
                .And.NotContain("-filePatching")
                .And.Contain($"-apiport={3305 + index}");
            args.Should().Contain($"-profiles=\"{Path.Combine(profiles, "DevRun_8c1f9d22")}hc{index}\"");
            helpers.IsSyntheticProcess(args).Should().BeTrue();
            SyntheticApiPorts.IsSynthetic(3305 + index).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Headless_clients_launch_on_the_mission_running_line_and_die_with_the_server()
    {
        var launcher = new Mock<IDevRunLauncher>();
        launcher.Setup(x => x.Launch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), 2))
                .Returns(new SyntheticLaunchResult(10, "p", "m", [new ProcessLaunch("exe", "hc1"), new ProcessLaunch("exe", "hc2")]));
        _processUtilities.Setup(x => x.LaunchManagedProcess("exe", "hc1")).Returns(11);
        _processUtilities.Setup(x => x.LaunchManagedProcess("exe", "hc2")).Returns(12);
        var serverAlive = true;
        _processUtilities.Setup(x => x.IsProcessAlive(10)).Returns(() => serverAlive);
        var released = new TaskCompletionSource();
        var gate = new Mock<IArmaSyntheticLaunchGate>();
        gate.Setup(x => x.TryAcquire(It.IsAny<string>())).Returns(true);
        gate.Setup(x => x.Release()).Callback(() => released.TrySetResult());
        var context = new Mock<IDevRunsContext>();
        context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, object>>>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        context.Setup(x => x.GetSingle(It.IsAny<Func<DomainDevRun, bool>>())).Returns(new DomainDevRun { Id = "id", Status = DevRunStatus.Running });
        var service = new DevRunService(
            launcher.Object,
            context.Object,
            _processUtilities.Object,
            gate.Object,
            new Mock<IVariablesService>().Object,
            new Mock<IUksfLogger>().Object,
            20,
            30
        );

        var runId = service.Trigger("diag_log 1;", [], null, null, 2).RunId;
        _processUtilities.Verify(x => x.LaunchManagedProcess(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        await service.AppendLogAsync(runId, DevRunLauncher.MissionRunningLogLine);
        await service.AppendLogAsync(runId, DevRunLauncher.MissionRunningLogLine);
        _processUtilities.Verify(x => x.LaunchManagedProcess("exe", "hc1"), Times.Once);
        _processUtilities.Verify(x => x.LaunchManagedProcess("exe", "hc2"), Times.Once);

        serverAlive = false;
        await released.Task.WaitAsync(TimeSpan.FromSeconds(5));

        _processUtilities.Verify(x => x.FindProcessById(11), Times.Once);
        _processUtilities.Verify(x => x.FindProcessById(12), Times.Once);
        _processUtilities.Verify(x => x.FindProcessById(10), Times.Never);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Trigger_rejects_headless_client_count_outside_zero_to_three(int headlessClients)
    {
        var service = new Mock<IDevRunService>();
        var controller = new DevRunController(service.Object, new Mock<IDevRunsContext>().Object);

        var result = controller.Trigger(new TriggerDevRunRequest("diag_log 1;", [], null, null, headlessClients));

        result.Should().BeOfType<BadRequestObjectResult>();
        service.VerifyNoOtherCalls();
    }
}
