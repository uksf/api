using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Operations;

public class OperationOrderDataServiceTests
{
    private readonly Mock<IMongoCollection<Opord>> _mockDataCollection;
    private readonly OperationOrderContext _operationOrderContext;

    public OperationOrderDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<Opord>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

        _operationOrderContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        Opord item1 = new() { Start = DateTime.UtcNow.AddDays(-1) };
        Opord item2 = new() { Start = DateTime.UtcNow.AddDays(-2) };
        Opord item3 = new() { Start = DateTime.UtcNow.AddDays(-3) };

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<Opord> { item1, item2, item3 });

        var subject = _operationOrderContext.Get();

        subject.Should().ContainInOrder(item3, item2, item1);
    }

    [Fact]
    public void ShouldGetOrderedCollectionByPredicate()
    {
        Opord item1 = new() { Description = "1", Start = DateTime.UtcNow.AddDays(-1) };
        Opord item2 = new() { Description = "2", Start = DateTime.UtcNow.AddDays(-2) };
        Opord item3 = new() { Description = "1", Start = DateTime.UtcNow.AddDays(-3) };

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<Opord> { item1, item2, item3 });

        var subject = _operationOrderContext.Get(x => x.Description == "1");

        subject.Should().ContainInOrder(item3, item1);
    }
}
