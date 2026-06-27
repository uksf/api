using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class IntelPagesControllerTests
{
    private readonly Mock<IIntelPagesContext> _mockContext = new();
    private readonly IntelPagesController _controller;

    public IntelPagesControllerTests()
    {
        _controller = new IntelPagesController(_mockContext.Object);
    }

    [Fact]
    public void Get_filters_by_scope_and_owner()
    {
        DomainIntelPage a = new() { Scope = IntelScope.Op, OwnerId = "op1", Title = "A" };
        DomainIntelPage b = new() { Scope = IntelScope.Campaign, OwnerId = "c1", Title = "B" };
        _mockContext.Setup(x => x.Get()).Returns([a, b]);

        var result = _controller.Get(IntelScope.Op, "op1").ToList();

        result.Should().ContainSingle().Which.Should().Be(a);
    }

    [Fact]
    public async Task Post_adds_intel_page()
    {
        DomainIntelPage a = new() { Title = "A" };
        await _controller.Post(a);
        _mockContext.Verify(x => x.Add(a), Times.Once);
    }
}
