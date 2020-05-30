using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Units;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Units;
using Xunit;
using UksfUnit = UKSF.Api.Models.Units.Unit;

namespace UKSF.Tests.Unit.Unit.Data.Units {
    public class UnitsDataServiceTests {
        [Fact]
        public void ShouldGetOrderedCollection() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<IUnitsDataService>> mockDataEventBus = new Mock<IDataEventBus<IUnitsDataService>>();
            Mock<IDataCollection<UksfUnit>> mockDataCollection = new Mock<IDataCollection<UksfUnit>>();

            UksfUnit rank1 = new UksfUnit {name = "Air Troop", order = 2};
            UksfUnit rank2 = new UksfUnit {name = "UKSF", order = 0};
            UksfUnit rank3 = new UksfUnit {name = "SAS", order = 1};

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<UksfUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<UksfUnit> {rank1, rank2, rank3});

            UnitsDataService unitsDataService = new UnitsDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            List<UksfUnit> subject = unitsDataService.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }

        [Fact]
        public void ShouldGetOrderedCollectionFromPredicate() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<IUnitsDataService>> mockDataEventBus = new Mock<IDataEventBus<IUnitsDataService>>();
            Mock<IDataCollection<UksfUnit>> mockDataCollection = new Mock<IDataCollection<UksfUnit>>();

            UksfUnit rank1 = new UksfUnit {name = "Air Troop", order = 3, type = UnitType.SECTION};
            UksfUnit rank2 = new UksfUnit {name = "Boat Troop", order = 2, type = UnitType.SECTION};
            UksfUnit rank3 = new UksfUnit {name = "UKSF", order = 0, type = UnitType.TASKFORCE};
            UksfUnit rank4 = new UksfUnit {name = "SAS", order = 1, type = UnitType.REGIMENT};

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<UksfUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<UksfUnit> {rank1, rank2, rank3, rank4});

            UnitsDataService unitsDataService = new UnitsDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            List<UksfUnit> subject = unitsDataService.Get(x => x.type == UnitType.SECTION);

            subject.Should().ContainInOrder(rank2, rank1);
        }
    }
}
