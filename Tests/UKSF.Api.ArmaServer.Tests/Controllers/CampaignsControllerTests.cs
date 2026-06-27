using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class CampaignsControllerTests
{
    private readonly Mock<ICampaignsContext> _mockContext = new();
    private readonly CampaignsController _controller;

    public CampaignsControllerTests()
    {
        _controller = new CampaignsController(_mockContext.Object);
    }

    [Fact]
    public void Get_returns_all_campaigns()
    {
        DomainCampaign a = new() { Name = "Op Storm" };
        _mockContext.Setup(x => x.Get()).Returns([a]);

        var result = _controller.Get();

        result.Should().Contain(a);
    }

    [Fact]
    public async Task Post_adds_a_campaign()
    {
        DomainCampaign a = new() { Name = "Op Storm" };

        await _controller.Post(a);

        _mockContext.Verify(x => x.Add(a), Times.Once);
    }

    [Fact]
    public async Task Delete_removes_a_campaign()
    {
        await _controller.Delete("abc");

        _mockContext.Verify(x => x.Delete("abc"), Times.Once);
    }
}
