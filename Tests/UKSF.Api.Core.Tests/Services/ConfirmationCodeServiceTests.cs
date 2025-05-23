using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

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
        DomainConfirmationCode confirmationCode = new() { Value = "test" };
        List<DomainConfirmationCode> confirmationCodeData = [confirmationCode];
        var actionParameters = JsonSerializer.Serialize(new object[] { confirmationCode.Id }, DefaultJsonSerializerOptions.Options);

        DomainScheduledJob scheduledJob = new() { ActionParameters = actionParameters };
        List<DomainScheduledJob> subject = [scheduledJob];

        _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
        _mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask);
        _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<DomainScheduledJob, bool>>()))
                             .Returns(Task.CompletedTask)
                             .Callback<Func<DomainScheduledJob, bool>>(x => subject.Remove(subject.FirstOrDefault(x)));

        await _confirmationCodeService.GetConfirmationCodeValue(confirmationCode.Id);

        subject.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldCreateConfirmationCode()
    {
        DomainConfirmationCode subject = null;

        _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<DomainConfirmationCode>()))
                                        .Returns(Task.CompletedTask)
                                        .Callback<DomainConfirmationCode>(x => subject = x);
        _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<object[]>()))
                             .Returns(Task.CompletedTask);

        await _confirmationCodeService.CreateConfirmationCode("test");

        subject.Should().NotBeNull();
        subject.Value.Should().Be("test");
    }

    [Fact]
    public async Task ShouldGetCorrectConfirmationCode()
    {
        DomainConfirmationCode confirmationCode1 = new() { Value = "test1" };
        DomainConfirmationCode confirmationCode2 = new() { Value = "test2" };
        List<DomainConfirmationCode> confirmationCodeData = [confirmationCode1, confirmationCode2];

        _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
        _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<DomainScheduledJob, bool>>()))
                             .Returns<Func<DomainScheduledJob, bool>>(x => Task.FromResult(new List<DomainScheduledJob>().FirstOrDefault(x)));

        var subject = await _confirmationCodeService.GetConfirmationCodeValue(confirmationCode2.Id);

        subject.Should().Be("test2");
    }

    [Fact]
    public async Task ShouldReturnCodeValue()
    {
        DomainConfirmationCode confirmationCode = new() { Value = "test" };
        List<DomainConfirmationCode> confirmationCodeData = [confirmationCode];

        _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
        _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<DomainScheduledJob, bool>>()))
                             .Returns<Func<DomainScheduledJob, bool>>(x => Task.FromResult(new List<DomainScheduledJob>().FirstOrDefault(x)));

        var subject = await _confirmationCodeService.GetConfirmationCodeValue(confirmationCode.Id);

        subject.Should().Be("test");
    }

    [Fact]
    public async Task ShouldReturnValidCodeId()
    {
        _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<DomainConfirmationCode>())).Returns(Task.CompletedTask);
        _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<object[]>()))
                             .Returns(Task.CompletedTask);

        var subject = await _confirmationCodeService.CreateConfirmationCode("test");

        subject.Should().HaveLength(24);
        ObjectId.TryParse(subject, out var _).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSetConfirmationCodeValue()
    {
        DomainConfirmationCode subject = null;

        _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<DomainConfirmationCode>()))
                                        .Returns(Task.CompletedTask)
                                        .Callback<DomainConfirmationCode>(x => subject = x);
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
        _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<DomainConfirmationCode>(null);

        var subject = await _confirmationCodeService.GetConfirmationCodeValue(id);

        subject.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ShouldThrowForCreateWhenValueNullOrEmpty(string value)
    {
        _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<DomainConfirmationCode>())).Returns(Task.CompletedTask);
        _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<object[]>()))
                             .Returns(Task.CompletedTask);

        Func<Task> act = async () => await _confirmationCodeService.CreateConfirmationCode(value);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
