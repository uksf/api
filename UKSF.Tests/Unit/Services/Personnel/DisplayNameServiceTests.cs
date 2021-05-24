using FluentAssertions;
using Moq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel
{
    public class DisplayNameServiceTests
    {
        private readonly DisplayNameService _displayNameService;
        private readonly Mock<IAccountContext> _mockAccountContext;
        private readonly Mock<IRanksContext> _mockRanksContext;

        public DisplayNameServiceTests()
        {
            _mockRanksContext = new();
            _mockAccountContext = new();

            _displayNameService = new(_mockAccountContext.Object, _mockRanksContext.Object);
        }

        [Fact]
        public void ShouldGetDisplayNameByAccount()
        {
            DomainAccount domainAccount = new() { Lastname = "Beswick", Firstname = "Tim" };

            string subject = _displayNameService.GetDisplayName(domainAccount);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetDisplayNameById()
        {
            DomainAccount domainAccount = new() { Lastname = "Beswick", Firstname = "Tim" };

            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(domainAccount);

            string subject = _displayNameService.GetDisplayName(domainAccount.Id);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetDisplayNameWithoutRank()
        {
            DomainAccount domainAccount = new() { Lastname = "Beswick", Firstname = "Tim" };

            string subject = _displayNameService.GetDisplayNameWithoutRank(domainAccount);

            subject.Should().Be("Beswick.T");
        }

        [Fact]
        public void ShouldGetDisplayNameWithRank()
        {
            DomainAccount domainAccount = new() { Lastname = "Beswick", Firstname = "Tim", Rank = "Squadron Leader" };
            Rank rank = new() { Abbreviation = "SqnLdr" };

            _mockRanksContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(rank);

            string subject = _displayNameService.GetDisplayName(domainAccount);

            subject.Should().Be("SqnLdr.Beswick.T");
        }

        [Fact]
        public void ShouldGetGuestWhenAccountHasNoName()
        {
            DomainAccount domainAccount = new();

            string subject = _displayNameService.GetDisplayNameWithoutRank(domainAccount);

            subject.Should().Be("Guest");
        }

        [Fact]
        public void ShouldGetGuestWhenAccountIsNull()
        {
            string subject = _displayNameService.GetDisplayNameWithoutRank(null);

            subject.Should().Be("Guest");
        }

        [Fact]
        public void ShouldGetNoDisplayNameWhenAccountNotFound()
        {
            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<DomainAccount>(null);

            string subject = _displayNameService.GetDisplayName("5e39336e1b92ee2d14b7fe08");

            subject.Should().Be("5e39336e1b92ee2d14b7fe08");
        }
    }
}
