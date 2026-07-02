using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Context;

public class OpsContextTests
{
    private readonly Mock<IMongoCollection<DomainOp>> _mockDataCollection = new();
    private readonly OpsContext _opsContext;

    public OpsContextTests()
    {
        Mock<IMongoCollectionFactory> mockFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        mockFactory.Setup(x => x.CreateMongoCollection<DomainOp>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _opsContext = new OpsContext(mockFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_return_ops_from_collection()
    {
        DomainOp op1 = new() { Title = "Alpha" };
        DomainOp op2 = new() { Title = "Bravo" };
        _mockDataCollection.Setup(x => x.Get()).Returns([op1, op2]);

        var subject = _opsContext.Get();

        subject.Should().Contain([op1, op2]);
    }
}
