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
        private readonly Mock<IConfirmationCodeDataService> mockConfirmationCodeDataService;
        private readonly Mock<ISchedulerService> mockSchedulerService;
        private readonly ConfirmationCodeService confirmationCodeService;

        public ConfirmationCodeServiceTests() {
            mockConfirmationCodeDataService = new Mock<IConfirmationCodeDataService>();
            mockSchedulerService = new Mock<ISchedulerService>();
            confirmationCodeService = new ConfirmationCodeService(mockConfirmationCodeDataService.Object, mockSchedulerService.Object);
        }

        [Fact]
        public async Task ShouldReturnValidCodeId() {
            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
            mockSchedulerService.Setup(x => x.Create(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<ScheduledJobType>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            string id = await confirmationCodeService.CreateConfirmationCode("test");

            id.Should().HaveLength(24);
            ObjectId.TryParse(id, out ObjectId _).Should().BeTrue();
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

        [Theory, InlineData(""), InlineData(null)]
        public async Task ShouldReturnEmptyStringWhenNoIdOrNull(string id) {
            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<Func<ConfirmationCode, bool>>())).Returns<Func<ConfirmationCode, bool>>(x => new List<ConfirmationCode>().FirstOrDefault(x));

            string subject = await confirmationCodeService.GetConfirmationCode(id);

            subject.Should().Be(string.Empty);
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
            mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>())).Returns(Task.CompletedTask).Callback<Func<ScheduledJob, bool>>(x => subject.Remove(subject.FirstOrDefault(x)));

            await confirmationCodeService.GetConfirmationCode(confirmationCode.id);

            subject.Should().BeEmpty();
        }
    }
}
