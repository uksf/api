using FluentAssertions;
using Moq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel {
    public class DisplayNameServiceTests {
        private readonly DisplayNameService _displayNameService;
        private readonly Mock<IAccountContext> _mockAccountContext;
        private readonly Mock<IRanksContext> _mockRanksContext;

        public DisplayNameServiceTests() {
            _mockRanksContext = new Mock<IRanksContext>();
            _mockAccountContext = new Mock<IAccountContext>();

            _displayNameService = new DisplayNameService(_mockAccountContext.Object, _mockRanksContext.Object);
        }

        [Fact]
        public void ShouldGetDisplayNameByAccount() {
            Account account = new() { Lastname = "Beswick", Firstname = "Tim" };

            string subject = _displayNameService.GetDisplayName(account);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetDisplayNameById() {
            Account account = new() { Lastname = "Beswick", Firstname = "Tim" };

            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(account);

            string subject = _displayNameService.GetDisplayName(account.Id);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetDisplayNameWithoutRank() {
            Account account = new() { Lastname = "Beswick", Firstname = "Tim" };

            string subject = _displayNameService.GetDisplayNameWithoutRank(account);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetDisplayNameWithRank() {
            Account account = new() { Lastname = "Beswick", Firstname = "Tim", Rank = "Squadron Leader" };
            Rank rank = new() { Abbreviation = "SqnLdr" };

            _mockRanksContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(rank);

            string subject = _displayNameService.GetDisplayName(account);

            subject.Should().Be("SqnLdr.Beswick.T");
        }

        [Fact]
        public void ShouldGetGuestWhenAccountHasNoName() {
            Account account = new();

            string subject = _displayNameService.GetDisplayNameWithoutRank(account);

            subject.Should().Be("Guest");
        }

        [Fact]
        public void ShouldGetGuestWhenAccountIsNull() {
            string subject = _displayNameService.GetDisplayNameWithoutRank(null);

            subject.Should().Be("Guest");
        }

        [Fact]
        public void ShouldGetNoDisplayNameWhenAccountNotFound() {
            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<Account>(null);

            string subject = _displayNameService.GetDisplayName("5e39336e1b92ee2d14b7fe08");

            subject.Should().Be("5e39336e1b92ee2d14b7fe08");
        }
    }
}
