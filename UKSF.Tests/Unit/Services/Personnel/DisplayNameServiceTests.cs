using FluentAssertions;
using Moq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Personnel.Models;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel {
    public class DisplayNameServiceTests {
        private readonly Mock<IRanksDataService> mockRanksDataService;
        private readonly Mock<IAccountDataService> mockAccountDataService;
        private readonly DisplayNameService displayNameService;

        public DisplayNameServiceTests() {
            mockRanksDataService = new Mock<IRanksDataService>();
            mockAccountDataService = new Mock<IAccountDataService>();
            Mock<IRanksService> mockRanksService = new Mock<IRanksService>();
            Mock<IAccountService> mockAccountService = new Mock<IAccountService>();

            mockRanksService.Setup(x => x.Data).Returns(mockRanksDataService.Object);
            mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);

            displayNameService = new DisplayNameService(mockRanksService.Object, mockAccountService.Object);
        }

        [Fact]
        public void ShouldGetDisplayNameById() {
            Account account = new Account {lastname = "Beswick", firstname = "Tim"};

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(account);

            string subject = displayNameService.GetDisplayName(account.id);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetNoDisplayNameWhenAccountNotFound() {
            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<Account>(null);

            string subject = displayNameService.GetDisplayName("5e39336e1b92ee2d14b7fe08");

            subject.Should().Be("5e39336e1b92ee2d14b7fe08");
        }

        [Fact]
        public void ShouldGetDisplayNameByAccount() {
            Account account = new Account {lastname = "Beswick", firstname = "Tim"};

            string subject = displayNameService.GetDisplayName(account);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetDisplayNameWithRank() {
            Account account = new Account {lastname = "Beswick", firstname = "Tim", rank = "Squadron Leader"};
            Rank rank = new Rank {abbreviation = "SqnLdr"};

            mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(rank);

            string subject = displayNameService.GetDisplayName(account);

            subject.Should().Be("SqnLdr.Beswick.T");
        }

        [Fact]
        public void ShouldGetDisplayNameWithoutRank() {
            Account account = new Account {lastname = "Beswick", firstname = "Tim"};

            string subject = displayNameService.GetDisplayNameWithoutRank(account);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetGuestWhenAccountHasNoName() {
            Account account = new Account();

            string subject = displayNameService.GetDisplayNameWithoutRank(account);

            subject.Should().Be("Guest");
        }

        [Fact]
        public void ShouldGetGuestWhenAccountIsNull() {
            string subject = displayNameService.GetDisplayNameWithoutRank(null);

            subject.Should().Be("Guest");
        }
    }
}
