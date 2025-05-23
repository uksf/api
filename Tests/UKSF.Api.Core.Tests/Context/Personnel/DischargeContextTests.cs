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

namespace UKSF.Api.Core.Tests.Context.Personnel;

public class DischargeContextTests
{
    [Fact]
    public void Should_get_collection_in_order()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IMongoCollection<DomainDischargeCollection>> mockDataCollection = new();
        Mock<IVariablesService> mockVariablesService = new();

        DomainDischargeCollection item1 = new() { Discharges = [new DomainDischarge { Timestamp = DateTime.UtcNow.AddDays(-3) }] };
        DomainDischargeCollection item2 = new()
        {
            Discharges = [new DomainDischarge { Timestamp = DateTime.UtcNow.AddDays(-10) }, new DomainDischarge { Timestamp = DateTime.UtcNow.AddDays(-1) }]
        };
        DomainDischargeCollection item3 = new()
        {
            Discharges = [new DomainDischarge { Timestamp = DateTime.UtcNow.AddDays(-5) }, new DomainDischarge { Timestamp = DateTime.UtcNow.AddDays(-2) }]
        };

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainDischargeCollection>(It.IsAny<string>())).Returns(mockDataCollection.Object);
        mockDataCollection.Setup(x => x.Get())
        .Returns(
            new List<DomainDischargeCollection>
            {
                item1,
                item2,
                item3
            }
        );
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        DischargeContext dischargeContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);

        var subject = dischargeContext.Get();

        subject.Should().ContainInOrder(item2, item3, item1);
    }
}
