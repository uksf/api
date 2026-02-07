using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class AccountServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly AccountService _subject;

    public AccountServiceTests()
    {
        _subject = new AccountService(_mockAccountContext.Object, _mockHttpContextService.Object);
    }

    [Fact]
    public void GetUserAccount_ShouldReturnAccountForCurrentUser()
    {
        var userId = "user1";
        var expected = new DomainAccount { Id = userId, Firstname = "Tim" };
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(userId);
        _mockAccountContext.Setup(x => x.GetSingle(userId)).Returns(expected);

        var result = _subject.GetUserAccount();

        result.Should().BeSameAs(expected);
        _mockHttpContextService.Verify(x => x.GetUserId(), Times.Once);
        _mockAccountContext.Verify(x => x.GetSingle(userId), Times.Once);
    }

    [Fact]
    public void GetUserAccount_ShouldReturnNull_WhenAccountDoesNotExist()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("nonexistent");
        _mockAccountContext.Setup(x => x.GetSingle("nonexistent")).Returns((DomainAccount)null);

        var result = _subject.GetUserAccount();

        result.Should().BeNull();
    }
}
