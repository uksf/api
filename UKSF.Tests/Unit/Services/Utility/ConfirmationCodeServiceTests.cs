using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using Newtonsoft.Json;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility {
    public class ConfirmationCodeServiceTests {
        private readonly ConfirmationCodeService _confirmationCodeService;
        private readonly Mock<IConfirmationCodeContext> _mockConfirmationCodeDataService;
        private readonly Mock<ISchedulerService> _mockSchedulerService;

        public ConfirmationCodeServiceTests() {
            _mockConfirmationCodeDataService = new Mock<IConfirmationCodeContext>();
            _mockSchedulerService = new Mock<ISchedulerService>();
            _confirmationCodeService = new ConfirmationCodeService(_mockConfirmationCodeDataService.Object, _mockSchedulerService.Object);
        }

        [Theory, InlineData(""), InlineData("1"), InlineData(null)]
        public async Task ShouldReturnEmptyStringWhenBadIdOrNull(string id) {
            _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<ConfirmationCode>(null);

            string subject = await _confirmationCodeService.GetConfirmationCode(id);

            subject.Should().Be(string.Empty);
        }

        [Theory, InlineData(null), InlineData("")]
        public async Task ShouldThrowForCreateWhenValueNullOrEmpty(string value) {
            _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
            _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            Func<Task> act = async () => await _confirmationCodeService.CreateConfirmationCode(value);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task ShouldCancelScheduledJob() {
            ConfirmationCode confirmationCode = new() { Value = "test" };
            List<ConfirmationCode> confirmationCodeData = new() { confirmationCode };
            string actionParameters = JsonConvert.SerializeObject(new object[] { confirmationCode.Id });

            ScheduledJob scheduledJob = new() { ActionParameters = actionParameters };
            List<ScheduledJob> subject = new() { scheduledJob };

            _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
            _mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>()))
                                 .Returns(Task.CompletedTask)
                                 .Callback<Func<ScheduledJob, bool>>(x => subject.Remove(subject.FirstOrDefault(x)));

            await _confirmationCodeService.GetConfirmationCode(confirmationCode.Id);

            subject.Should().BeEmpty();
        }

        [Fact]
        public async Task ShouldCreateConfirmationCode() {
            ConfirmationCode subject = null;

            _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask).Callback<ConfirmationCode>(x => subject = x);
            _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            await _confirmationCodeService.CreateConfirmationCode("test");

            subject.Should().NotBeNull();
            subject.Value.Should().Be("test");
        }

        [Fact]
        public async Task ShouldGetCorrectConfirmationCode() {
            ConfirmationCode confirmationCode1 = new() { Value = "test1" };
            ConfirmationCode confirmationCode2 = new() { Value = "test2" };
            List<ConfirmationCode> confirmationCodeData = new() { confirmationCode1, confirmationCode2 };

            _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
            _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>())).Returns<Func<ScheduledJob, bool>>(x => Task.FromResult(new List<ScheduledJob>().FirstOrDefault(x)));

            string subject = await _confirmationCodeService.GetConfirmationCode(confirmationCode2.Id);

            subject.Should().Be("test2");
        }

        [Fact]
        public async Task ShouldReturnCodeValue() {
            ConfirmationCode confirmationCode = new() { Value = "test" };
            List<ConfirmationCode> confirmationCodeData = new() { confirmationCode };

            _mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.Id == x));
            _mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>())).Returns<Func<ScheduledJob, bool>>(x => Task.FromResult(new List<ScheduledJob>().FirstOrDefault(x)));

            string subject = await _confirmationCodeService.GetConfirmationCode(confirmationCode.Id);

            subject.Should().Be("test");
        }

        [Fact]
        public async Task ShouldReturnValidCodeId() {
            _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
            _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            string subject = await _confirmationCodeService.CreateConfirmationCode("test");

            subject.Should().HaveLength(24);
            ObjectId.TryParse(subject, out ObjectId _).Should().BeTrue();
        }

        [Fact]
        public async Task ShouldSetConfirmationCodeValue() {
            ConfirmationCode subject = null;

            _mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask).Callback<ConfirmationCode>(x => subject = x);
            _mockSchedulerService.Setup(x => x.CreateAndScheduleJob(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            await _confirmationCodeService.CreateConfirmationCode("test");

            subject.Should().NotBeNull();
            subject.Value.Should().Be("test");
        }
    }
}
