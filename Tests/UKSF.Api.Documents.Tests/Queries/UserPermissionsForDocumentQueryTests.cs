using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Documents.Queries;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;
using Xunit;

namespace UKSF.Api.Documents.Tests.Queries {
    public class UserPermissionsForDocumentQueryTests {
        private readonly Mock<IHttpContextService> _mockHttpContextService;
        private readonly IUserPermissionsForDocumentQuery _subject;
        private readonly string _userId = ObjectId.GenerateNewId().ToString();

        public UserPermissionsForDocumentQueryTests() {
            _mockHttpContextService = new();

            _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_userId);

            _subject = new UserPermissionsForDocumentQuery(_mockHttpContextService.Object);
        }

        [Fact]
        public void When_user_is_admin() {
            Given_user_is_admin();

            UserPermissionsForDocumentResult result = _subject.Execute(new(new()));

            result.CanView.Should().BeTrue();
            result.CanEdit.Should().BeTrue();
        }

        [Fact]
        public void When_user_is_creator() {
            Given_user_is_not_admin();

            UserPermissionsForDocumentResult result = _subject.Execute(new(new() { CreatorId = _userId }));

            result.CanView.Should().BeTrue();
            result.CanEdit.Should().BeTrue();
        }

        [Fact]
        public void When_user_is_not_admin_or_creator() {
            Given_user_is_not_admin();

            UserPermissionsForDocumentResult result = _subject.Execute(new(new()));

            result.CanView.Should().BeFalse();
            result.CanEdit.Should().BeFalse();
        }

        private void Given_user_is_admin() {
            _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.ADMIN)).Returns(true);
        }

        private void Given_user_is_not_admin() {
            _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.ADMIN)).Returns(false);
        }
    }
}
