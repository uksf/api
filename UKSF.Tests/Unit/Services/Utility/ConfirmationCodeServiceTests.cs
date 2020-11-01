using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using Newtonsoft.Json;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Utility;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Services.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility {
    public class ConfirmationCodeServiceTests {
        private readonly ConfirmationCodeService confirmationCodeService;
        private readonly Mock<IConfirmationCodeDataService> mockConfirmationCodeDataService;
        private readonly Mock<ISchedulerService> mockSchedulerService;

        public ConfirmationCodeServiceTests() {
            mockConfirmationCodeDataService = new Mock<IConfirmationCodeDataService>();
            mockSchedulerService = new Mock<ISchedulerService>();
            confirmationCodeService = new ConfirmationCodeService(mockConfirmationCodeDataService.Object, mockSchedulerService.Object);
        }

        [Theory, InlineData(""), InlineData("1"), InlineData(null)]
        public async Task ShouldReturnEmptyStringWhenBadIdOrNull(string id) {
            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<ConfirmationCode>(null);

            string subject = await confirmationCodeService.GetConfirmationCode(id);

            subject.Should().Be(string.Empty);
        }

        [Fact]
        public async Task ShouldSetConfirmationCodeValue() {
            ConfirmationCode subject = null;

            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask).Callback<ConfirmationCode>(x => subject = x);
            mockSchedulerService.Setup(x => x.CreateAndSchedule(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            await confirmationCodeService.CreateConfirmationCode("test");

            subject.Should().NotBeNull();
            subject.value.Should().Be("test");
        }

        [Theory, InlineData(null), InlineData("")]
        public async Task ShouldThrowForCreateWhenValueNullOrEmpty(string value) {
            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
            mockSchedulerService.Setup(x => x.CreateAndSchedule(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            Func<Task> act = async () => await confirmationCodeService.CreateConfirmationCode(value);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task ShouldCancelScheduledJob() {
            ConfirmationCode confirmationCode = new ConfirmationCode {value = "test"};
            List<ConfirmationCode> confirmationCodeData = new List<ConfirmationCode> {confirmationCode};
            string actionParameters = JsonConvert.SerializeObject(new object[] {confirmationCode.id});

            ScheduledJob scheduledJob = new ScheduledJob {actionParameters = actionParameters};
            List<ScheduledJob> subject = new List<ScheduledJob> {scheduledJob};

            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.id == x));
            mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask);
            mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>()))
                                .Returns(Task.CompletedTask)
                                .Callback<Func<ScheduledJob, bool>>(x => subject.Remove(subject.FirstOrDefault(x)));

            await confirmationCodeService.GetConfirmationCode(confirmationCode.id);

            subject.Should().BeEmpty();
        }

        [Fact]
        public async Task ShouldCreateConfirmationCode() {
            ConfirmationCode subject = null;

            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask).Callback<ConfirmationCode>(x => subject = x);
            mockSchedulerService.Setup(x => x.CreateAndSchedule(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            await confirmationCodeService.CreateConfirmationCode("test");

            subject.Should().NotBeNull();
            subject.value.Should().Be("test");
        }

        [Fact]
        public async Task ShouldGetCorrectConfirmationCode() {
            ConfirmationCode confirmationCode1 = new ConfirmationCode {value = "test1"};
            ConfirmationCode confirmationCode2 = new ConfirmationCode {value = "test2"};
            List<ConfirmationCode> confirmationCodeData = new List<ConfirmationCode> {confirmationCode1, confirmationCode2};

            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.id == x));
            mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask).Callback<string>(x => confirmationCodeData.RemoveAll(y => y.id == x));
            mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>())).Returns<Func<ScheduledJob, bool>>(x => Task.FromResult(new List<ScheduledJob>().FirstOrDefault(x)));

            string subject = await confirmationCodeService.GetConfirmationCode(confirmationCode2.id);

            subject.Should().Be("test2");
        }

        [Fact]
        public async Task ShouldReturnCodeValue() {
            ConfirmationCode confirmationCode = new ConfirmationCode {value = "test"};
            List<ConfirmationCode> confirmationCodeData = new List<ConfirmationCode> {confirmationCode};

            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => confirmationCodeData.FirstOrDefault(y => y.id == x));
            mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask).Callback<string>(x => confirmationCodeData.RemoveAll(y => y.id == x));
            mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>())).Returns<Func<ScheduledJob, bool>>(x => Task.FromResult(new List<ScheduledJob>().FirstOrDefault(x)));

            string subject = await confirmationCodeService.GetConfirmationCode(confirmationCode.id);

            subject.Should().Be("test");
        }

        [Fact]
        public async Task ShouldReturnValidCodeId() {
            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
            mockSchedulerService.Setup(x => x.CreateAndSchedule(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            string subject = await confirmationCodeService.CreateConfirmationCode("test");

            subject.Should().HaveLength(24);
            ObjectId.TryParse(subject, out ObjectId _).Should().BeTrue();
        }
    }
}
