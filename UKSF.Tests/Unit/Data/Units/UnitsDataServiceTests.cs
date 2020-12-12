using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using Xunit;
using UksfUnit = UKSF.Api.Personnel.Models.Unit;

namespace UKSF.Tests.Unit.Data.Units {
    public class UnitsDataServiceTests {
        [Fact]
        public void Should_get_collection_in_order() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<IEventBus> mockEventBus = new();
            Mock<IMongoCollection<UksfUnit>> mockDataCollection = new();

            UksfUnit rank1 = new() { Name = "Air Troop", Order = 2 };
            UksfUnit rank2 = new() { Name = "UKSF", Order = 0 };
            UksfUnit rank3 = new() { Name = "SAS", Order = 1 };

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<UksfUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<UksfUnit> { rank1, rank2, rank3 });

            UnitsContext unitsContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);

            IEnumerable<UksfUnit> subject = unitsContext.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }

        [Fact]
        public void ShouldGetOrderedCollectionFromPredicate() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<IEventBus> mockEventBus = new();
            Mock<IMongoCollection<UksfUnit>> mockDataCollection = new();

            UksfUnit rank1 = new() { Name = "Air Troop", Order = 3, Branch = UnitBranch.COMBAT };
            UksfUnit rank2 = new() { Name = "Boat Troop", Order = 2, Branch = UnitBranch.COMBAT };
            UksfUnit rank3 = new() { Name = "UKSF", Order = 0, Branch = UnitBranch.AUXILIARY };
            UksfUnit rank4 = new() { Name = "SAS", Order = 1, Branch = UnitBranch.AUXILIARY };

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<UksfUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<UksfUnit> { rank1, rank2, rank3, rank4 });

            UnitsContext unitsContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);

            IEnumerable<UksfUnit> subject = unitsContext.Get(x => x.Branch == UnitBranch.COMBAT);

            subject.Should().ContainInOrder(rank2, rank1);
        }
    }
}
