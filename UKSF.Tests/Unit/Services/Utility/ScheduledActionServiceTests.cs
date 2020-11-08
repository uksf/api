using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Utility.ScheduledActions;
using UKSF.Api.Utility.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility {
    public class ScheduledActionServiceTests {
        [Fact]
        public void ShouldRegisterActions() {
            Mock<IDeleteExpiredConfirmationCodeAction> mockDeleteExpiredConfirmationCodeAction = new Mock<IDeleteExpiredConfirmationCodeAction>();
            mockDeleteExpiredConfirmationCodeAction.Setup(x => x.Name).Returns("TestAction");

            IScheduledActionService scheduledActionService = new ScheduledActionService();
            scheduledActionService.RegisterScheduledActions(new HashSet<IScheduledAction> {mockDeleteExpiredConfirmationCodeAction.Object});

            IScheduledAction subject = scheduledActionService.GetScheduledAction("TestAction");

            subject.Should().Be(mockDeleteExpiredConfirmationCodeAction.Object);
        }

        [Fact]
        public void ShouldOverwriteRegisteredActions() {
            Mock<IDeleteExpiredConfirmationCodeAction> mockDeleteExpiredConfirmationCodeAction1 = new Mock<IDeleteExpiredConfirmationCodeAction>();
            Mock<IDeleteExpiredConfirmationCodeAction> mockDeleteExpiredConfirmationCodeAction2 = new Mock<IDeleteExpiredConfirmationCodeAction>();
            mockDeleteExpiredConfirmationCodeAction1.Setup(x => x.Name).Returns("TestAction");
            mockDeleteExpiredConfirmationCodeAction2.Setup(x => x.Name).Returns("TestAction");

            IScheduledActionService scheduledActionService = new ScheduledActionService();
            scheduledActionService.RegisterScheduledActions(new HashSet<IScheduledAction> {mockDeleteExpiredConfirmationCodeAction1.Object});
            scheduledActionService.RegisterScheduledActions(new HashSet<IScheduledAction> {mockDeleteExpiredConfirmationCodeAction2.Object});

            IScheduledAction subject = scheduledActionService.GetScheduledAction("TestAction");

            subject.Should().Be(mockDeleteExpiredConfirmationCodeAction2.Object);
        }

        [Fact]
        public void ShouldThrowWhenActionNotFound() {
            IScheduledActionService scheduledActionService = new ScheduledActionService();

            Action act = () => scheduledActionService.GetScheduledAction("TestAction");

            act.Should().Throw<ArgumentException>();
        }
    }
}
