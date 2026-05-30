using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class ScheduledActionsControllerTests
{
    private readonly Mock<IScheduledActionFactory> _factory = new();
    private readonly ScheduledActionsController _subject;

    public ScheduledActionsControllerTests()
    {
        _subject = new ScheduledActionsController(_factory.Object);
    }

    [Fact]
    public async Task Run_WhenActionExists_InvokesAndReturnsOk()
    {
        var action = new Mock<IScheduledAction>();
        _factory.Setup(x => x.GetScheduledAction("ActionFoo")).Returns(action.Object);

        var result = await _subject.Run("ActionFoo");

        result.Should().BeOfType<OkResult>();
        action.Verify(x => x.Run(), Times.Once);
    }

    [Fact]
    public async Task Run_WhenActionNotFound_ReturnsNotFound()
    {
        _factory.Setup(x => x.GetScheduledAction("Missing")).Throws(new ArgumentException("nope"));

        var result = await _subject.Run("Missing");

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
