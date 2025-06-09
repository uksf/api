using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class ScheduledActionServiceTests
{
    [Fact]
    public void ShouldOverwriteRegisteredActions()
    {
        Mock<IActionDeleteExpiredConfirmationCode> mockDeleteExpiredConfirmationCodeAction1 = new();
        Mock<IActionDeleteExpiredConfirmationCode> mockDeleteExpiredConfirmationCodeAction2 = new();
        mockDeleteExpiredConfirmationCodeAction1.Setup(x => x.Name).Returns("TestAction");
        mockDeleteExpiredConfirmationCodeAction2.Setup(x => x.Name).Returns("TestAction");

        IScheduledActionFactory scheduledActionFactory = new ScheduledActionFactory();
        scheduledActionFactory.RegisterScheduledActions(new HashSet<IScheduledAction> { mockDeleteExpiredConfirmationCodeAction1.Object });
        scheduledActionFactory.RegisterScheduledActions(new HashSet<IScheduledAction> { mockDeleteExpiredConfirmationCodeAction2.Object });

        var subject = scheduledActionFactory.GetScheduledAction("TestAction");

        subject.Should().Be(mockDeleteExpiredConfirmationCodeAction2.Object);
    }

    [Fact]
    public void ShouldRegisterActions()
    {
        Mock<IActionDeleteExpiredConfirmationCode> mockDeleteExpiredConfirmationCodeAction = new();
        mockDeleteExpiredConfirmationCodeAction.Setup(x => x.Name).Returns("TestAction");

        IScheduledActionFactory scheduledActionFactory = new ScheduledActionFactory();
        scheduledActionFactory.RegisterScheduledActions(new HashSet<IScheduledAction> { mockDeleteExpiredConfirmationCodeAction.Object });

        var subject = scheduledActionFactory.GetScheduledAction("TestAction");

        subject.Should().Be(mockDeleteExpiredConfirmationCodeAction.Object);
    }

    [Fact]
    public void ShouldThrowWhenActionNotFound()
    {
        IScheduledActionFactory scheduledActionFactory = new ScheduledActionFactory();

        Action act = () => scheduledActionFactory.GetScheduledAction("TestAction");

        act.Should().Throw<ArgumentException>();
    }
}
