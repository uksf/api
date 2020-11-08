using System;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Personnel.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions {
    public class DeleteExpiredConfirmationCodeActionTests {
        private readonly Mock<IConfirmationCodeDataService> mockConfirmationCodeDataService;
        private readonly Mock<IConfirmationCodeService> mockConfirmationCodeService;
        private IActionDeleteExpiredConfirmationCode actionDeleteExpiredConfirmationCode;

        public DeleteExpiredConfirmationCodeActionTests() {
            mockConfirmationCodeDataService = new Mock<IConfirmationCodeDataService>();
            mockConfirmationCodeService = new Mock<IConfirmationCodeService>();

            mockConfirmationCodeService.Setup(x => x.Data).Returns(mockConfirmationCodeDataService.Object);
        }

        [Fact]
        public void ShouldDeleteCorrectId() {
            string id = ObjectId.GenerateNewId().ToString();

            actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(mockConfirmationCodeService.Object);

            actionDeleteExpiredConfirmationCode.Run(id);

            mockConfirmationCodeDataService.Verify(x => x.Delete(id), Times.Once);
        }

        [Fact]
        public void ShouldReturnActionName() {
            actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(mockConfirmationCodeService.Object);

            string subject = actionDeleteExpiredConfirmationCode.Name;

            subject.Should().Be("DeleteExpiredConfirmationCodeAction");
        }

        [Fact]
        public void ShouldThrowForNoId() {
            actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(mockConfirmationCodeService.Object);

            Action act = () => actionDeleteExpiredConfirmationCode.Run();

            act.Should().Throw<ArgumentException>();
        }
    }
}
