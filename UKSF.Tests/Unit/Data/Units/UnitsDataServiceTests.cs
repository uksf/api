using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;
using Xunit;
using UksfUnit = UKSF.Api.Personnel.Models.Unit;

namespace UKSF.Tests.Unit.Data.Units {
    public class UnitsDataServiceTests {
        [Fact]
        public void Should_get_collection_in_order() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<UksfUnit>> mockDataEventBus = new Mock<IDataEventBus<UksfUnit>>();
            Mock<IDataCollection<UksfUnit>> mockDataCollection = new Mock<IDataCollection<UksfUnit>>();

            UksfUnit rank1 = new UksfUnit { name = "Air Troop", order = 2 };
            UksfUnit rank2 = new UksfUnit { name = "UKSF", order = 0 };
            UksfUnit rank3 = new UksfUnit { name = "SAS", order = 1 };

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<UksfUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<UksfUnit> { rank1, rank2, rank3 });

            UnitsDataService unitsDataService = new UnitsDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            IEnumerable<UksfUnit> subject = unitsDataService.Get();

            subject.Should().ContainInOrder(rank2, rank3, rank1);
        }

        [Fact]
        public void ShouldGetOrderedCollectionFromPredicate() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<UksfUnit>> mockDataEventBus = new Mock<IDataEventBus<UksfUnit>>();
            Mock<IDataCollection<UksfUnit>> mockDataCollection = new Mock<IDataCollection<UksfUnit>>();

            UksfUnit rank1 = new UksfUnit { name = "Air Troop", order = 3, branch = UnitBranch.COMBAT };
            UksfUnit rank2 = new UksfUnit { name = "Boat Troop", order = 2, branch = UnitBranch.COMBAT  };
            UksfUnit rank3 = new UksfUnit { name = "UKSF", order = 0, branch = UnitBranch.AUXILIARY  };
            UksfUnit rank4 = new UksfUnit { name = "SAS", order = 1, branch = UnitBranch.AUXILIARY  };

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<UksfUnit>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<UksfUnit> { rank1, rank2, rank3, rank4 });

            UnitsDataService unitsDataService = new UnitsDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            IEnumerable<UksfUnit> subject = unitsDataService.Get(x => x.branch == UnitBranch.COMBAT);

            subject.Should().ContainInOrder(rank2, rank1);
        }
    }
}
