using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class OperationsServiceTests
{
    private readonly Mock<IOperationsContext> _mockOperationsContext = new();
    private readonly Mock<ICampaignMissionsContext> _mockCampaignMissionsContext = new();
    private readonly Mock<ICampaignMissionsService> _mockCampaignMissionsService = new();
    private readonly Mock<IIntelPagesContext> _mockIntelPagesContext = new();
    private readonly OperationsService _service;

    public OperationsServiceTests()
    {
        _service = new OperationsService(
            _mockOperationsContext.Object,
            _mockCampaignMissionsContext.Object,
            _mockCampaignMissionsService.Object,
            _mockIntelPagesContext.Object
        );
    }

    [Fact]
    public async Task DeleteOperation_deletes_missions_then_operation_intel_then_operation()
    {
        DomainMission m1 = new() { Id = "m1", OperationId = "op1" };
        DomainMission m2 = new() { Id = "m2", OperationId = "op1" };
        DomainMission other = new() { Id = "m3", OperationId = "op2" };
        _mockCampaignMissionsContext.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>()))
                                    .Returns<Func<DomainMission, bool>>(predicate => new[] { m1, m2, other }.Where(predicate));

        Expression<Func<DomainIntelPage, bool>> captured = null;
        _mockIntelPagesContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()))
                              .Callback<Expression<Func<DomainIntelPage, bool>>>(e => captured = e)
                              .Returns(Task.CompletedTask);

        await _service.DeleteOperation("op1");

        _mockCampaignMissionsService.Verify(x => x.DeleteMission("m1"), Times.Once);
        _mockCampaignMissionsService.Verify(x => x.DeleteMission("m2"), Times.Once);
        _mockCampaignMissionsService.Verify(x => x.DeleteMission("m3"), Times.Never);
        _mockIntelPagesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()), Times.Once);
        _mockOperationsContext.Verify(x => x.Delete("op1"), Times.Once);

        var predicate = captured.Compile();
        predicate(new DomainIntelPage { Scope = IntelScope.Operation, OwnerId = "op1" }).Should().BeTrue();
        predicate(new DomainIntelPage { Scope = IntelScope.Operation, OwnerId = "op2" }).Should().BeFalse();
        predicate(new DomainIntelPage { Scope = IntelScope.Mission, OwnerId = "op1" }).Should().BeFalse();
        predicate(new DomainIntelPage { Scope = IntelScope.Campaign, OwnerId = "op1" }).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteOperation_propagates_partial_mission_delete_failure()
    {
        DomainMission m1 = new() { Id = "m1", OperationId = "op1" };
        DomainMission m2 = new() { Id = "m2", OperationId = "op1" };
        _mockCampaignMissionsContext.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>()))
                                    .Returns<Func<DomainMission, bool>>(predicate => new[] { m1, m2 }.Where(predicate));
        _mockCampaignMissionsService.Setup(x => x.DeleteMission("m1")).ThrowsAsync(new InvalidOperationException("fail"));

        var act = () => _service.DeleteOperation("op1");

        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockCampaignMissionsService.Verify(x => x.DeleteMission("m2"), Times.Never);
        _mockOperationsContext.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        _mockIntelPagesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()), Times.Never);
    }
}
