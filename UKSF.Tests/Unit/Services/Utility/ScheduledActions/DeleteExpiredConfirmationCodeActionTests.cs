using System;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Interfaces.Utility.ScheduledActions;
using UKSF.Api.Services.Utility.ScheduledActions;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions {
    public class DeleteExpiredConfirmationCodeActionTests {
        private readonly Mock<IConfirmationCodeDataService> mockConfirmationCodeDataService;
        private readonly Mock<IConfirmationCodeService> mockConfirmationCodeService;
        private IDeleteExpiredConfirmationCodeAction deleteExpiredConfirmationCodeAction;

        public DeleteExpiredConfirmationCodeActionTests() {
            mockConfirmationCodeDataService = new Mock<IConfirmationCodeDataService>();
            mockConfirmationCodeService = new Mock<IConfirmationCodeService>();

            mockConfirmationCodeService.Setup(x => x.Data).Returns(mockConfirmationCodeDataService.Object);
        }

        [Fact]
        public void ShouldDeleteCorrectId() {
            string id = ObjectId.GenerateNewId().ToString();

            deleteExpiredConfirmationCodeAction = new DeleteExpiredConfirmationCodeAction(mockConfirmationCodeService.Object);

            deleteExpiredConfirmationCodeAction.Run(id);

            mockConfirmationCodeDataService.Verify(x => x.Delete(id), Times.Once);
        }

        [Fact]
        public void ShouldReturnActionName() {
            deleteExpiredConfirmationCodeAction = new DeleteExpiredConfirmationCodeAction(mockConfirmationCodeService.Object);

            string subject = deleteExpiredConfirmationCodeAction.Name;

            subject.Should().Be("DeleteExpiredConfirmationCodeAction");
        }

        [Fact]
        public void ShouldThrowForNoId() {
            deleteExpiredConfirmationCodeAction = new DeleteExpiredConfirmationCodeAction(mockConfirmationCodeService.Object);

            Action act = () => deleteExpiredConfirmationCodeAction.Run();

            act.Should().Throw<ArgumentException>();
        }
    }
}
