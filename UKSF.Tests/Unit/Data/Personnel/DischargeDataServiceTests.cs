using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel;

public class DischargeDataServiceTests
{
    [Fact]
    public void Should_get_collection_in_order()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IMongoCollection<DischargeCollection>> mockDataCollection = new();

        DischargeCollection item1 = new() { Discharges = new() { new() { Timestamp = DateTime.UtcNow.AddDays(-3) } } };
        DischargeCollection item2 = new()
        {
            Discharges = new() { new() { Timestamp = DateTime.UtcNow.AddDays(-10) }, new() { Timestamp = DateTime.UtcNow.AddDays(-1) } }
        };
        DischargeCollection item3 = new()
        {
            Discharges = new() { new() { Timestamp = DateTime.UtcNow.AddDays(-5) }, new() { Timestamp = DateTime.UtcNow.AddDays(-2) } }
        };

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DischargeCollection>(It.IsAny<string>())).Returns(mockDataCollection.Object);
        mockDataCollection.Setup(x => x.Get()).Returns(new List<DischargeCollection> { item1, item2, item3 });

        DischargeContext dischargeContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);

        var subject = dischargeContext.Get();

        subject.Should().ContainInOrder(item2, item3, item1);
    }
}
