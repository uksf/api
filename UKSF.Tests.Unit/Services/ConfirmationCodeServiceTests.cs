using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Utility;
using UKSF.Api.Services.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Services {
    public class ConfirmationCodeServiceTests {
        [Fact]
        public async Task ShouldReturnCodeId() {
            Mock<IConfirmationCodeDataService> mockConfirmationCodeDataService = new Mock<IConfirmationCodeDataService>();
            Mock<ISchedulerService> mockSchedulerService = new Mock<ISchedulerService>();
            ConfirmationCodeService confirmationCodeService = new ConfirmationCodeService(mockConfirmationCodeDataService.Object, mockSchedulerService.Object);

            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask);
            
            mockSchedulerService.Setup(x => x.Create(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<ScheduledJobType>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);

            string id = await confirmationCodeService.CreateConfirmationCode("test");

            id.Should().HaveLength(24);
            ObjectId.TryParse(id, out ObjectId _).Should().BeTrue();
        }

        [Fact]
        public async Task ShouldReturnCodeValue() {
            Mock<IConfirmationCodeDataService> mockConfirmationCodeDataService = new Mock<IConfirmationCodeDataService>();
            Mock<ISchedulerService> mockSchedulerService = new Mock<ISchedulerService>();
            ConfirmationCodeService confirmationCodeService = new ConfirmationCodeService(mockConfirmationCodeDataService.Object, mockSchedulerService.Object);
            
            List<ConfirmationCode> confirmationCodeData = new List<ConfirmationCode>();

            mockConfirmationCodeDataService.Setup(x => x.Add(It.IsAny<ConfirmationCode>())).Returns(Task.CompletedTask).Callback<ConfirmationCode>(x => confirmationCodeData.Add(x));
            mockConfirmationCodeDataService.Setup(x => x.GetSingle(It.IsAny<Func<ConfirmationCode, bool>>())).Returns(() => confirmationCodeData.First());
            mockConfirmationCodeDataService.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask).Callback<string>(x => confirmationCodeData.RemoveAll(y => y.id == x));
            
            mockSchedulerService.Setup(x => x.Create(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<ScheduledJobType>(), It.IsAny<string>(), It.IsAny<object[]>())).Returns(Task.CompletedTask);
            mockSchedulerService.Setup(x => x.Cancel(It.IsAny<Func<ScheduledJob, bool>>())).Returns(Task.CompletedTask);

            string id = await confirmationCodeService.CreateConfirmationCode("test");
            string subject = await confirmationCodeService.GetConfirmationCode(id);

            subject.Should().Be("test");
        }
    }
}
