using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Units;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using Xunit;
using UUnit = UKSF.Api.Models.Units.Unit;

namespace UKSF.Tests.Unit.Data.Units {
    public class UnitsDataServiceTests {
        [Fact]
        public void ShouldGetOrderedCollection() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<IUnitsDataService>> mockDataEventBus = new Mock<IDataEventBus<IUnitsDataService>>();
            Mock<IDataCollection<UUnit>> mockDataCollection = new Mock<IDataCollection<UUnit>>();

            UUnit rank1 = new UUnit {name = "Air Troop", order = 2};
            UUnit rank2 = new UUnit {name = "UKSF", order = 0};
            UUnit rank3 = new UUnit {name = "SAS", order = 1};

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<UUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<UUnit> {rank1, rank2, rank3});

            UnitsDataService unitsDataService = new UnitsDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            List<UUnit> subject = unitsDataService.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }
    }
}
