using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class RanksControllerTests
{
    private readonly Mock<IRanksContext> _mockRanksContext;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly RanksController _controller;

    public RanksControllerTests()
    {
        _mockRanksContext = new Mock<IRanksContext>();
        _mockAccountContext = new Mock<IAccountContext>();
        var mockAssignmentService = new Mock<IAssignmentService>();
        var mockNotificationsService = new Mock<INotificationsService>();
        var mockLogger = new Mock<IUksfLogger>();

        _controller = new RanksController(
            _mockAccountContext.Object,
            _mockRanksContext.Object,
            mockAssignmentService.Object,
            mockNotificationsService.Object,
            mockLogger.Object
        );
    }

    [Fact]
    public async Task UpdateOrder_Should_Update_Order_When_Ranks_Are_Reordered()
    {
        // Arrange: 3 ranks in original order 0,1,2
        var rankA = new DomainRank
        {
            Id = "a",
            Name = "Private",
            Order = 0
        };
        var rankB = new DomainRank
        {
            Id = "b",
            Name = "Corporal",
            Order = 1
        };
        var rankC = new DomainRank
        {
            Id = "c",
            Name = "Sergeant",
            Order = 2
        };

        // Frontend sends them in new order: C, A, B (user dragged C to top)
        var newOrder = new List<DomainRank>
        {
            new()
            {
                Id = "c",
                Name = "Sergeant",
                Order = 2
            },
            new()
            {
                Id = "a",
                Name = "Private",
                Order = 0
            },
            new()
            {
                Id = "b",
                Name = "Corporal",
                Order = 1
            }
        };

        // GetSingle(name) returns the rank with its CURRENT order from the database
        _mockRanksContext.Setup(x => x.GetSingle("Sergeant")).Returns(rankC);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(rankA);
        _mockRanksContext.Setup(x => x.GetSingle("Corporal")).Returns(rankB);

        var updatedRanks = new List<DomainRank>
        {
            new()
            {
                Id = "c",
                Name = "Sergeant",
                Order = 0
            },
            new()
            {
                Id = "a",
                Name = "Private",
                Order = 1
            },
            new()
            {
                Id = "b",
                Name = "Corporal",
                Order = 2
            }
        };
        _mockRanksContext.Setup(x => x.Get()).Returns(updatedRanks);

        // Act
        var result = await _controller.UpdateOrder(newOrder);

        // Assert: all 3 ranks should have been updated
        _mockRanksContext.Verify(x => x.Update("c", It.IsAny<System.Linq.Expressions.Expression<System.Func<DomainRank, int>>>(), 0), Times.Once);
        _mockRanksContext.Verify(x => x.Update("a", It.IsAny<System.Linq.Expressions.Expression<System.Func<DomainRank, int>>>(), 1), Times.Once);
        _mockRanksContext.Verify(x => x.Update("b", It.IsAny<System.Linq.Expressions.Expression<System.Func<DomainRank, int>>>(), 2), Times.Once);

        result.Should().BeEquivalentTo(updatedRanks);
    }
}
