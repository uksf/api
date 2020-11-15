using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel {
    public class DischargeDataServiceTests {
        [Fact]
        public void Should_get_collection_in_order() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<DischargeCollection>> mockDataEventBus = new Mock<IDataEventBus<DischargeCollection>>();
            Mock<IDataCollection<DischargeCollection>> mockDataCollection = new Mock<IDataCollection<DischargeCollection>>();

            DischargeCollection item1 = new DischargeCollection { discharges = new List<Discharge> { new Discharge { timestamp = DateTime.Now.AddDays(-3) } } };
            DischargeCollection item2 = new DischargeCollection {
                discharges = new List<Discharge> { new Discharge { timestamp = DateTime.Now.AddDays(-10) }, new Discharge { timestamp = DateTime.Now.AddDays(-1) } }
            };
            DischargeCollection item3 = new DischargeCollection {
                discharges = new List<Discharge> { new Discharge { timestamp = DateTime.Now.AddDays(-5) }, new Discharge { timestamp = DateTime.Now.AddDays(-2) } }
            };

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<DischargeCollection>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<DischargeCollection> { item1, item2, item3 });

            DischargeDataService dischargeDataService = new DischargeDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            IEnumerable<DischargeCollection> subject = dischargeDataService.Get();

            subject.Should().ContainInOrder(item2, item3, item1);
        }
    }
}
