using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class SchedulerServiceTests
{
    private readonly Mock<ISchedulerContext> _mockContext;
    private readonly Mock<IScheduledActionFactory> _mockFactory;
    private readonly Mock<IClock> _mockClock;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly SchedulerService _subject;

    public SchedulerServiceTests()
    {
        _mockContext = new Mock<ISchedulerContext>();
        _mockFactory = new Mock<IScheduledActionFactory>();
        _mockClock = new Mock<IClock>();
        _mockLogger = new Mock<IUksfLogger>();
        _mockClock.Setup(x => x.UtcNow()).Returns(DateTime.UtcNow);

        _subject = new SchedulerService(_mockContext.Object, _mockFactory.Object, _mockClock.Object, _mockLogger.Object);
    }

    [Fact]
    public void Load_ShouldScheduleAllJobsFromContext()
    {
        var jobs = new List<DomainScheduledJob>
        {
            new() { Action = "Action1", Next = DateTime.UtcNow.AddHours(1) }, new() { Action = "Action2", Next = DateTime.UtcNow.AddHours(2) }
        };
        _mockContext.Setup(x => x.Get()).Returns(jobs);

        var act = () => _subject.Load();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task CreateScheduledJob_ShouldReturnExistingJob_WhenActionAlreadyExists()
    {
        var existing = new DomainScheduledJob { Action = "TestAction", Interval = TimeSpan.FromMinutes(5) };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainScheduledJob, bool>>())).Returns(existing);

        var result = await _subject.CreateScheduledJob("TestAction", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromMinutes(5));

        result.Should().Be(existing);
        _mockContext.Verify(x => x.Add(It.IsAny<DomainScheduledJob>()), Times.Never);
    }

    [Fact]
    public async Task CreateScheduledJob_ShouldUpdateInterval_WhenExistingJobHasDifferentInterval()
    {
        var existing = new DomainScheduledJob { Action = "TestAction", Interval = TimeSpan.FromMinutes(5) };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainScheduledJob, bool>>())).Returns(existing);

        await _subject.CreateScheduledJob("TestAction", DateTime.UtcNow.AddMinutes(10), TimeSpan.FromMinutes(10));

        _mockContext.Verify(
            x => x.Update(existing.Id, It.IsAny<System.Linq.Expressions.Expression<Func<DomainScheduledJob, TimeSpan>>>(), TimeSpan.FromMinutes(10)),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateScheduledJob_ShouldCreateNewJob_WhenNoExisting()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainScheduledJob, bool>>())).Returns((DomainScheduledJob)null);

        var result = await _subject.CreateScheduledJob("NewAction", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromMinutes(5));

        result.Action.Should().Be("NewAction");
        result.Repeat.Should().BeTrue();
        _mockContext.Verify(x => x.Add(It.IsAny<DomainScheduledJob>()), Times.Once);
    }

    [Fact]
    public async Task CreateScheduledJob_ShouldNotSetRepeat_WhenIntervalIsZero()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainScheduledJob, bool>>())).Returns((DomainScheduledJob)null);

        var result = await _subject.CreateScheduledJob("OneTimeAction", DateTime.UtcNow.AddMinutes(5), TimeSpan.Zero);

        result.Repeat.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_ShouldDoNothing_WhenJobNotFound()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainScheduledJob, bool>>())).Returns((DomainScheduledJob)null);

        await _subject.Cancel(x => x.Action == "NonExistent");

        _mockContext.Verify(x => x.Delete(It.IsAny<DomainScheduledJob>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_ShouldDeleteJob_WhenFound()
    {
        var job = new DomainScheduledJob { Action = "TestAction" };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainScheduledJob, bool>>())).Returns(job);

        await _subject.Cancel(x => x.Action == "TestAction");

        _mockContext.Verify(x => x.Delete(job), Times.Once);
    }

    [Fact]
    public async Task Schedule_ShouldExecuteAction_WhenJobTimeHasPassed()
    {
        var mockAction = new Mock<IScheduledAction>();
        mockAction.Setup(x => x.Run(It.IsAny<object[]>())).Returns(Task.CompletedTask);
        _mockFactory.Setup(x => x.GetScheduledAction("TestAction")).Returns(mockAction.Object);
        _mockClock.Setup(x => x.UtcNow()).Returns(DateTime.UtcNow);

        var job = new DomainScheduledJob
        {
            Action = "TestAction",
            Next = DateTime.UtcNow.AddMilliseconds(-100),
            Repeat = false
        };
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainScheduledJob> { job });
        _mockContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(job);

        _subject.Load();

        await Task.Delay(TimeSpan.FromSeconds(1));

        mockAction.Verify(x => x.Run(It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task Schedule_ShouldLogError_WhenActionThrows()
    {
        var mockAction = new Mock<IScheduledAction>();
        mockAction.Setup(x => x.Run(It.IsAny<object[]>())).ThrowsAsync(new InvalidOperationException("action failed"));
        _mockFactory.Setup(x => x.GetScheduledAction("FailAction")).Returns(mockAction.Object);
        _mockClock.Setup(x => x.UtcNow()).Returns(DateTime.UtcNow);

        var job = new DomainScheduledJob
        {
            Action = "FailAction",
            Next = DateTime.UtcNow.AddMilliseconds(-100),
            Repeat = false
        };
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainScheduledJob> { job });
        _mockContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(job);

        _subject.Load();

        await Task.Delay(TimeSpan.FromSeconds(1));

        _mockLogger.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task Schedule_ShouldDeleteNonRepeatingJob_AfterExecution()
    {
        var mockAction = new Mock<IScheduledAction>();
        mockAction.Setup(x => x.Run(It.IsAny<object[]>())).Returns(Task.CompletedTask);
        _mockFactory.Setup(x => x.GetScheduledAction("OneTimeAction")).Returns(mockAction.Object);
        _mockClock.Setup(x => x.UtcNow()).Returns(DateTime.UtcNow);

        var job = new DomainScheduledJob
        {
            Action = "OneTimeAction",
            Next = DateTime.UtcNow.AddMilliseconds(-100),
            Repeat = false
        };
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainScheduledJob> { job });
        _mockContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(job);

        _subject.Load();

        await Task.Delay(TimeSpan.FromSeconds(1));

        _mockContext.Verify(x => x.Delete(job), Times.Once);
    }

    [Fact]
    public async Task CreateAndScheduleJob_ShouldCreateAndSchedule()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainScheduledJob, bool>>())).Returns((DomainScheduledJob)null);
        _mockClock.Setup(x => x.UtcNow()).Returns(DateTime.UtcNow);

        await _subject.CreateAndScheduleJob("TestAction", DateTime.UtcNow.AddHours(1), TimeSpan.FromHours(1));

        _mockContext.Verify(x => x.Add(It.IsAny<DomainScheduledJob>()), Times.Once);
    }
}
