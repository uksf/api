using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.ScheduledActions;

public class ActionLaunchDueOpsTests
{
    private static readonly DateTime Now = new(2026, 6, 13, 18, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IOpsContext> _mockOpsContext = new();
    private readonly Mock<IOpsService> _mockOpsService = new();
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IClock> _mockClock = new();
    private readonly ActionLaunchDueOps _action;

    public ActionLaunchDueOpsTests()
    {
        Mock<ISchedulerService> mockSchedulerService = new();
        Mock<IHostEnvironment> mockHostEnvironment = new();
        _mockClock.Setup(x => x.UtcNow()).Returns(Now);
        _mockGameServersService.Setup(x => x.GetServer(It.IsAny<string>())).Returns(new DomainGameServer { Name = "Main Server" });

        _action = new ActionLaunchDueOps(
            mockSchedulerService.Object,
            mockHostEnvironment.Object,
            _mockClock.Object,
            _mockOpsContext.Object,
            _mockOpsService.Object,
            _mockGameServersService.Object,
            _mockLogger.Object
        );
    }

    private void SetupOps(params DomainOp[] ops)
    {
        _mockOpsContext.Setup(x => x.Get(It.IsAny<Func<DomainOp, bool>>()))
                       .Returns<Func<DomainOp, bool>>(predicate => ops.Where(predicate));
    }

    [Fact]
    public async Task Run_launches_a_due_op()
    {
        DomainOp op = new()
        {
            Id = "op1", Title = "Alpha", ServerId = "s1", MissionName = "m.Altis.pbo",
            Status = OpStatus.Scheduled, AutoLaunch = true, LaunchedAt = null, ScheduledTime = Now.AddMinutes(-5)
        };
        SetupOps(op);
        _mockOpsService.Setup(x => x.LaunchOpAsync(op, "Scheduler")).ReturnsAsync([]);

        await _action.Run();

        _mockOpsService.Verify(x => x.LaunchOpAsync(op, "Scheduler"), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("auto-launched")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Run_skips_op_not_due_yet()
    {
        DomainOp op = new()
        {
            Id = "op1", Title = "Alpha", Status = OpStatus.Scheduled, AutoLaunch = true, LaunchedAt = null, ScheduledTime = Now.AddMinutes(5)
        };
        SetupOps(op);

        await _action.Run();

        _mockOpsService.Verify(x => x.LaunchOpAsync(It.IsAny<DomainOp>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_skips_already_launched_op()
    {
        DomainOp op = new()
        {
            Id = "op1", Title = "Alpha", Status = OpStatus.Scheduled, AutoLaunch = true,
            LaunchedAt = Now.AddMinutes(-100), ScheduledTime = Now.AddMinutes(-5)
        };
        SetupOps(op);

        await _action.Run();

        _mockOpsService.Verify(x => x.LaunchOpAsync(It.IsAny<DomainOp>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_skips_op_without_autolaunch_enabled()
    {
        DomainOp op = new()
        {
            Id = "op1", Title = "Alpha", Status = OpStatus.Scheduled, AutoLaunch = false, LaunchedAt = null, ScheduledTime = Now.AddMinutes(-5)
        };
        SetupOps(op);

        await _action.Run();

        _mockOpsService.Verify(x => x.LaunchOpAsync(It.IsAny<DomainOp>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_skips_op_beyond_the_grace_window()
    {
        DomainOp op = new()
        {
            Id = "op1", Title = "Alpha", Status = OpStatus.Scheduled, AutoLaunch = true, LaunchedAt = null, ScheduledTime = Now.AddMinutes(-45)
        };
        SetupOps(op);

        await _action.Run();

        _mockOpsService.Verify(x => x.LaunchOpAsync(It.IsAny<DomainOp>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_catches_a_launch_failure_and_continues()
    {
        DomainOp op = new()
        {
            Id = "op1", Title = "Alpha", ServerId = "s1", MissionName = "m.Altis.pbo",
            Status = OpStatus.Scheduled, AutoLaunch = true, LaunchedAt = null, ScheduledTime = Now.AddMinutes(-5)
        };
        SetupOps(op);
        _mockOpsService.Setup(x => x.LaunchOpAsync(op, "Scheduler")).ThrowsAsync(new Exception("boom"));

        var act = () => _action.Run();

        await act.Should().NotThrowAsync();
        _mockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
    }
}
