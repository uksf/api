using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Data.Units;

public class UnitsDataServiceTests
{
    [Fact]
    public void Should_get_collection_in_order()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IMongoCollection<DomainUnit>> mockDataCollection = new();
        Mock<IVariablesService> mockVariablesService = new();

        DomainUnit rank1 = new() { Name = "Air Troop", Order = 2 };
        DomainUnit rank2 = new() { Name = "UKSF", Order = 0 };
        DomainUnit rank3 = new() { Name = "SAS", Order = 1 };

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
        mockDataCollection.Setup(x => x.Get()).Returns(new List<DomainUnit> { rank1, rank2, rank3 });
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        UnitsContext unitsContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);

        var subject = unitsContext.Get();

        subject.Should().ContainInOrder(rank2, rank3, rank1);
    }

    [Fact]
    public void ShouldGetOrderedCollectionFromPredicate()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IMongoCollection<DomainUnit>> mockDataCollection = new();
        Mock<IVariablesService> mockVariablesService = new();

        DomainUnit rank1 = new()
        {
            Name = "Air Troop",
            Order = 3,
            Branch = UnitBranch.Combat
        };
        DomainUnit rank2 = new()
        {
            Name = "Boat Troop",
            Order = 2,
            Branch = UnitBranch.Combat
        };
        DomainUnit rank3 = new()
        {
            Name = "UKSF",
            Order = 0,
            Branch = UnitBranch.Auxiliary
        };
        DomainUnit rank4 = new()
        {
            Name = "SAS",
            Order = 1,
            Branch = UnitBranch.Auxiliary
        };

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
        mockDataCollection.Setup(x => x.Get()).Returns(new List<DomainUnit> { rank1, rank2, rank3, rank4 });
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        UnitsContext unitsContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);

        var subject = unitsContext.Get(x => x.Branch == UnitBranch.Combat);

        subject.Should().ContainInOrder(rank2, rank1);
    }
}
