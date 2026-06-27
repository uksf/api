using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
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
        mockFactory.Setup(x => x.CreateMongoCollection<DomainOp>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        _opsContext = new OpsContext(mockFactory.Object, mockEventBus.Object);
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
