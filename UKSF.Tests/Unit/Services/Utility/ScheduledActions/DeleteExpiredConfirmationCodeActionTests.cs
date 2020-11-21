using System;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.ScheduledActions;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions {
    public class DeleteExpiredConfirmationCodeActionTests {
        private readonly Mock<IConfirmationCodeContext> _mockConfirmationCodeContext = new();
        private IActionDeleteExpiredConfirmationCode _actionDeleteExpiredConfirmationCode;

        [Fact]
        public void When_deleting_confirmation_code() {
            string id = ObjectId.GenerateNewId().ToString();

            _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeContext.Object);

            _actionDeleteExpiredConfirmationCode.Run(id);

            _mockConfirmationCodeContext.Verify(x => x.Delete(id), Times.Once);
        }

        [Fact]
        public void When_deleting_confirmation_code_with_no_id() {
            _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeContext.Object);

            Action act = () => _actionDeleteExpiredConfirmationCode.Run();

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void When_getting_action_name() {
            _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeContext.Object);

            string subject = _actionDeleteExpiredConfirmationCode.Name;

            subject.Should().Be("ActionDeleteExpiredConfirmationCode");
        }
    }
}
