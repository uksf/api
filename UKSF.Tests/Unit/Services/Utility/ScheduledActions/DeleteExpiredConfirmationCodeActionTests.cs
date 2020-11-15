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
        private readonly Mock<IConfirmationCodeDataService> _mockConfirmationCodeDataService;
        private readonly Mock<IConfirmationCodeService> _mockConfirmationCodeService;
        private IActionDeleteExpiredConfirmationCode _actionDeleteExpiredConfirmationCode;

        public DeleteExpiredConfirmationCodeActionTests() {
            _mockConfirmationCodeDataService = new Mock<IConfirmationCodeDataService>();
            _mockConfirmationCodeService = new Mock<IConfirmationCodeService>();

            _mockConfirmationCodeService.Setup(x => x.Data).Returns(_mockConfirmationCodeDataService.Object);
        }

        [Fact]
        public void When_deleting_confirmation_code() {
            string id = ObjectId.GenerateNewId().ToString();

            _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeService.Object);

            _actionDeleteExpiredConfirmationCode.Run(id);

            _mockConfirmationCodeDataService.Verify(x => x.Delete(id), Times.Once);
        }

        [Fact]
        public void When_deleting_confirmation_code_with_no_id() {
            _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeService.Object);

            Action act = () => _actionDeleteExpiredConfirmationCode.Run();

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void When_getting_action_name() {
            _actionDeleteExpiredConfirmationCode = new ActionDeleteExpiredConfirmationCode(_mockConfirmationCodeService.Object);

            string subject = _actionDeleteExpiredConfirmationCode.Name;

            subject.Should().Be("ActionDeleteExpiredConfirmationCode");
        }
    }
}
