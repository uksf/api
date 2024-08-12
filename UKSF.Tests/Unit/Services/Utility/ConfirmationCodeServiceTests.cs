using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility;

public class ConfirmationCodeServiceTests
{
    private readonly ConfirmationCodeService _confirmationCodeService;
    private readonly Mock<IConfirmationCodeContext> _mockConfirmationCodeDataService;
    private readonly Mock<ISchedulerService> _mockSchedulerService;

    public ConfirmationCodeServiceTests()
    {
        _mockConfirmationCodeDataService = new Mock<IConfirmationCodeContext>();
        _mockSchedulerService = new Mock<ISchedulerService>();

        var mockClock = new Mock<IClock>();
        mockClock.Setup(x => x.UtcNow()).Returns(DateTime.UtcNow);

        _confirmationCodeService = new ConfirmationCodeService(_mockConfirmationCodeDataService.Object, _mockSchedulerService.Object, mockClock.Object);
    }

    [Fact]
    public async Task ShouldCancelScheduledJob()
    {
        ConfirmationCode confirmationCode = new() { Value = "test" };
        List<ConfirmationCode> confirmationCodeData = [confirmationCode];
        var actionParameters = JsonSerializer.Serialize(new object[] { confirmationCode.Id }, DefaultJsonSerializerOptions.Options);

        ScheduledJob scheduledJob = new() { ActionParameters = actionParameters };
        List<ScheduledJob> subject = [scheduledJob];

        _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
        _mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask);
        _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>()))
                             .Returns(Task.CompletedTask)
                             .Callback<Func<ScheduledJob, bool>>(x => subject.Remove(subject.FirstOrDefault(x)));

        await _confirmationCodeService.GetConfirmationCodeValue(confirmationCode.Id);

        subject.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldCreateConfirmationCode()
    {
        ConfirmationCode subject = null;

        _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>()))
                                        .Returns(Task.CompletedTask)
                                        .Callback<ConfirmationCode>(x => subject = x);
        _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<object[]>()))
                             .Returns(Task.CompletedTask);

        await _confirmationCodeService.CreateConfirmationCode("test");

        subject.Should().NotBeNull();
        subject.Value.Should().Be("test");
    }

    [Fact]
    public async Task ShouldGetCorrectConfirmationCode()
    {
        ConfirmationCode confirmationCode1 = new() { Value = "test1" };
        ConfirmationCode confirmationCode2 = new() { Value = "test2" };
        List<ConfirmationCode> confirmationCodeData = [confirmationCode1, confirmationCode2];

        _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
        _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>()))
                             .Returns<Func<ScheduledJob, bool>>(x => Task.FromResult(new List<ScheduledJob>().FirstOrDefault(x)));

        var subject = await _confirmationCodeService.GetConfirmationCodeValue(confirmationCode2.Id);

        subject.Should().Be("test2");
    }

    [Fact]
    public async Task ShouldReturnCodeValue()
    {
        ConfirmationCode confirmationCode = new() { Value = "test" };
        List<ConfirmationCode> confirmationCodeData = [confirmationCode];

        _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
        _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>()))
                             .Returns<Func<ScheduledJob, bool>>(x => Task.FromResult(new List<ScheduledJob>().FirstOrDefault(x)));

        var subject = await _confirmationCodeService.GetConfirmationCodeValue(confirmationCode.Id);

        subject.Should().Be("test");
    }

    [Fact]
    public async Task ShouldReturnValidCodeId()
    {
        _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
        _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<object[]>()))
                             .Returns(Task.CompletedTask);

        var subject = await _confirmationCodeService.CreateConfirmationCode("test");

        subject.Should().HaveLength(24);
        ObjectId.TryParse(subject, out var _).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSetConfirmationCodeValue()
    {
        ConfirmationCode subject = null;

        _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>()))
                                        .Returns(Task.CompletedTask)
                                        .Callback<ConfirmationCode>(x => subject = x);
        _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<object[]>()))
                             .Returns(Task.CompletedTask);

        await _confirmationCodeService.CreateConfirmationCode("test");

        subject.Should().NotBeNull();
        subject.Value.Should().Be("test");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData(null)]
    public async Task ShouldReturnEmptyStringWhenBadIdOrNull(string id)
    {
        _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<ConfirmationCode>(null);

        var subject = await _confirmationCodeService.GetConfirmationCodeValue(id);

        subject.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ShouldThrowForCreateWhenValueNullOrEmpty(string value)
    {
        _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
        _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<object[]>()))
                             .Returns(Task.CompletedTask);

        Func<Task> act = async () => await _confirmationCodeService.CreateConfirmationCode(value);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
