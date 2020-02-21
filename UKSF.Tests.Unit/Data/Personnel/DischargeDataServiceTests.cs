using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Personnel;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel {
    public class DischargeDataServiceTests {
        [Fact]
        public void ShouldGetOrderedCollection() {
            Mock<IDataCollection> mockDataCollection = new Mock<IDataCollection>();
            Mock<IDataEventBus<IDischargeDataService>> mockDataEventBus = new Mock<IDataEventBus<IDischargeDataService>>();

            DischargeDataService dischargeDataService = new DischargeDataService(mockDataCollection.Object, mockDataEventBus.Object);

            DischargeCollection dischargeCollection1 = new DischargeCollection {discharges = new List<Discharge> {new Discharge {timestamp = DateTime.Now.AddDays(-3)}}};
            DischargeCollection dischargeCollection2 = new DischargeCollection {discharges = new List<Discharge> {new Discharge {timestamp = DateTime.Now.AddDays(-10)}, new Discharge {timestamp = DateTime.Now.AddDays(-1)}}};
            DischargeCollection dischargeCollection3 = new DischargeCollection {discharges = new List<Discharge> {new Discharge {timestamp = DateTime.Now.AddDays(-5)}, new Discharge {timestamp = DateTime.Now.AddDays(-2)}}};

            mockDataCollection.Setup(x => x.Get<DischargeCollection>()).Returns(new List<DischargeCollection> {dischargeCollection1, dischargeCollection2, dischargeCollection3});

            List<DischargeCollection> subject = dischargeDataService.Get();

            subject.Should().ContainInOrder(dischargeCollection2, dischargeCollection3, dischargeCollection1);
        }
    }
}
