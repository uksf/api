using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Context.Operations;

public class OperationOrderContextTests
{
    private readonly Mock<IMongoCollection<DomainOpord>> _mockDataCollection;
    private readonly OperationOrderContext _operationOrderContext;

    public OperationOrderContextTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new Mock<IMongoCollection<DomainOpord>>();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainOpord>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _operationOrderContext = new OperationOrderContext(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        DomainOpord item1 = new() { Start = DateTime.UtcNow.AddDays(-1) };
        DomainOpord item2 = new() { Start = DateTime.UtcNow.AddDays(-2) };
        DomainOpord item3 = new() { Start = DateTime.UtcNow.AddDays(-3) };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(
            new List<DomainOpord>
            {
                item1,
                item2,
                item3
            }
        );

        var subject = _operationOrderContext.Get();

        subject.Should().ContainInOrder(item3, item2, item1);
    }

    [Fact]
    public void ShouldGetOrderedCollectionByPredicate()
    {
        DomainOpord item1 = new() { Description = "1", Start = DateTime.UtcNow.AddDays(-1) };
        DomainOpord item2 = new() { Description = "2", Start = DateTime.UtcNow.AddDays(-2) };
        DomainOpord item3 = new() { Description = "1", Start = DateTime.UtcNow.AddDays(-3) };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(
            new List<DomainOpord>
            {
                item1,
                item2,
                item3
            }
        );

        var subject = _operationOrderContext.Get(x => x.Description == "1");

        subject.Should().ContainInOrder(item3, item1);
    }
}
