using System;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.ScheduledActions;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions;

public class DeleteExpiredConfirmationCodeActionTests
{
    private readonly Mock<IConfirmationCodeContext> _mockConfirmationCodeContext = new();
    private IActionDeleteExpiredConfirmationCode _actionDeleteExpiredConfirmationCode;

    [Fact]
    public async Task When_deleting_confirmation_code()
    {
        var id = ObjectId.GenerateNewId().ToString();

        _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeContext.Object);

        await _actionDeleteExpiredConfirmationCode.Run(id);

        _mockConfirmationCodeContext.Verify(x => x.Delete(id), Times.Once);
    }

    [Fact]
    public async Task When_deleting_confirmation_code_with_no_id()
    {
        _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeContext.Object);

        var act = async () => await _actionDeleteExpiredConfirmationCode.Run();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void When_getting_action_name()
    {
        _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeContext.Object);

        var subject = _actionDeleteExpiredConfirmationCode.Name;

        subject.Should().Be("ActionDeleteExpiredConfirmationCode");
    }
}
