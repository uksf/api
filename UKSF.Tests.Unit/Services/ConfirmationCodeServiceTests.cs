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
using UKSF.Api.Services.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Services {
    public class ConfirmationCodeServiceTests {
        private readonly ConfirmationCodeService confirmationCodeService;
        private readonly Mock<IConfirmationCodeDataService> mockConfirmationCodeDataService;
        private readonly Mock<ISchedulerService> mockSchedulerService;

        public ConfirmationCodeServiceTests() {
            mockConfirmationCodeDataService = new Mock<IConfirmationCodeDataService>();
            mockSchedulerService = new Mock<ISchedulerService>();
            confirmationCodeService = new ConfirmationCodeService(mockConfirmationCodeDataService.Object, mockSchedulerService.Object);
        }

        [Theory, InlineData(""), InlineData(null)]
        public async Task ShouldReturnEmptyStringWhenNoIdOrNull(string id) {
            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<Func<ConfirmationCode, bool>>()))
                                           .Returns<Func<ConfirmationCode, bool>>(x => new List<ConfirmationCode>().FirstOrDefault(x));

            string subject = await confirmationCodeService.GetConfirmationCode(id);

            subject.Should().Be(string.Empty);
        }

        [Theory, InlineData(true, ScheduledJobType.INTEGRATION), InlineData(false, ScheduledJobType.NORMAL)]
        public async Task ShouldUseCorrectScheduledJobType(bool integration, ScheduledJobType expectedJobType) {
            ScheduledJobType subject = ScheduledJobType.LOG_PRUNE;

            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
            mockSchedulerService.Setup(x => x.Create(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<ScheduledJobType>(), It.IsAny<string>(), It.IsAny<object[]>()))
                                .Returns(Task.CompletedTask)
                                .Callback<DateTime, TimeSpan, ScheduledJobType, string, object[]>((_1, _2, x, _3, _4) => subject = x);

            await confirmationCodeService.CreateConfirmationCode("test", integration);

            subject.Should().Be(expectedJobType);
        }

        [Fact]
        public async Task ShouldCancelScheduledJob() {
            ConfirmationCode confirmationCode = new ConfirmationCode {value = "test"};
            List<ConfirmationCode> confirmationCodeData = new List<ConfirmationCode> {confirmationCode};
            string actionParameters = JsonConvert.SerializeObject(new object[] {confirmationCode.id});

            ScheduledJob scheduledJob = new ScheduledJob {actionParameters = actionParameters};
            List<ScheduledJob> subject = new List<ScheduledJob> {scheduledJob};

            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<Func<ConfirmationCode, bool>>())).Returns<Func<ConfirmationCode, bool>>(x => confirmationCodeData.FirstOrDefault(x));
            mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask);
            mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>()))
                                .Returns(Task.CompletedTask)
                                .Callback<Func<ScheduledJob, bool>>(x => subject.Remove(subject.FirstOrDefault(x)));

            await confirmationCodeService.GetConfirmationCode(confirmationCode.id);

            subject.Should().BeEmpty();
        }

        [Fact]
        public async Task ShouldCreateConfirmationCode() {
            List<ConfirmationCode> subject = new List<ConfirmationCode>();

            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask).Callback<ConfirmationCode>(x => subject.Add(x));
            mockSchedulerService.Setup(x => x.Create(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<ScheduledJobType>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            await confirmationCodeService.CreateConfirmationCode("test");

            subject.Should().HaveCount(1);
            subject.First().value.Should().Be("test");
        }

        [Fact]
        public async Task ShouldGetCorrectConfirmationCode() {
            ConfirmationCode confirmationCode1 = new ConfirmationCode {value = "test1"};
            ConfirmationCode confirmationCode2 = new ConfirmationCode {value = "test2"};
            List<ConfirmationCode> confirmationCodeData = new List<ConfirmationCode> {confirmationCode1, confirmationCode2};

            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<Func<ConfirmationCode, bool>>())).Returns<Func<ConfirmationCode, bool>>(x => confirmationCodeData.FirstOrDefault(x));
            mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask).Callback<string>(x => confirmationCodeData.RemoveAll(y => y.id == x));
            mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>())).Returns<Func<ScheduledJob, bool>>(x => Task.FromResult(new List<ScheduledJob>().FirstOrDefault(x)));

            string subject = await confirmationCodeService.GetConfirmationCode(confirmationCode2.id);

            subject.Should().Be("test2");
        }

        [Fact]
        public async Task ShouldReturnCodeValue() {
            ConfirmationCode confirmationCode = new ConfirmationCode {value = "test"};
            List<ConfirmationCode> confirmationCodeData = new List<ConfirmationCode> {confirmationCode};

            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<Func<ConfirmationCode, bool>>())).Returns<Func<ConfirmationCode, bool>>(x => confirmationCodeData.FirstOrDefault(x));
            mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask).Callback<string>(x => confirmationCodeData.RemoveAll(y => y.id == x));
            mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>())).Returns<Func<ScheduledJob, bool>>(x => Task.FromResult(new List<ScheduledJob>().FirstOrDefault(x)));

            string subject = await confirmationCodeService.GetConfirmationCode(confirmationCode.id);

            subject.Should().Be("test");
        }

        [Fact]
        public async Task ShouldReturnValidCodeId() {
            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
            mockSchedulerService.Setup(x => x.Create(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<ScheduledJobType>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            string id = await confirmationCodeService.CreateConfirmationCode("test");

            id.Should().HaveLength(24);
            ObjectId.TryParse(id, out ObjectId _).Should().BeTrue();
        }
    }
}
