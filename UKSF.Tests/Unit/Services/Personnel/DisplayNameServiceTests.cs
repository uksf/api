using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel;

public class DisplayNameServiceTests
{
    private readonly DisplayNameService _displayNameService;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IRanksContext> _mockRanksContext;

    public DisplayNameServiceTests()
    {
        _mockRanksContext = new Mock<IRanksContext>();
        _mockAccountContext = new Mock<IAccountContext>();

        _displayNameService = new DisplayNameService(_mockAccountContext.Object, _mockRanksContext.Object);
    }

    [Fact]
    public void ShouldGetDisplayNameByAccount()
    {
        DomainAccount account = new() { Lastname = "Beswick", Firstname = "Tim" };

        var subject = _displayNameService.GetDisplayName(account);

        subject.Should().Be("Beswick.T");
    }

    [Fact]
    public void ShouldGetDisplayNameById()
    {
        DomainAccount account = new() { Lastname = "Beswick", Firstname = "Tim" };

        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(account);

        var subject = _displayNameService.GetDisplayName(account.Id);

        subject.Should().Be("Beswick.T");
    }

    [Fact]
    public void ShouldGetDisplayNameWithoutRank()
    {
        DomainAccount account = new() { Lastname = "Beswick", Firstname = "Tim" };

        var subject = _displayNameService.GetDisplayNameWithoutRank(account);

        subject.Should().Be("Beswick.T");
    }

    [Fact]
    public void ShouldGetDisplayNameWithRank()
    {
        DomainAccount account = new()
        {
            Lastname = "Beswick",
            Firstname = "Tim",
            Rank = "Squadron Leader"
        };
        DomainRank rank = new() { Abbreviation = "SqnLdr" };

        _mockRanksContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(rank);

        var subject = _displayNameService.GetDisplayName(account);

        subject.Should().Be("SqnLdr.Beswick.T");
    }

    [Fact]
    public void ShouldGetGuestWhenAccountHasNoName()
    {
        DomainAccount account = new();

        var subject = _displayNameService.GetDisplayNameWithoutRank(account);

        subject.Should().Be("Guest");
    }

    [Fact]
    public void ShouldGetGuestWhenAccountIsNull()
    {
        var subject = _displayNameService.GetDisplayNameWithoutRank((DomainAccount)null);

        subject.Should().Be("Guest");
    }

    [Fact]
    public void ShouldGetNoDisplayNameWhenAccountNotFound()
    {
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<DomainAccount>(null);

        var subject = _displayNameService.GetDisplayName("5e39336e1b92ee2d14b7fe08");

        subject.Should().Be("5e39336e1b92ee2d14b7fe08");
    }
}
