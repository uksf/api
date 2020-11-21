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
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<IDataEventBus<DischargeCollection>> mockDataEventBus = new();
            Mock<IMongoCollection<DischargeCollection>> mockDataCollection = new();

            DischargeCollection item1 = new() { Discharges = new List<Discharge> { new() { Timestamp = DateTime.Now.AddDays(-3) } } };
            DischargeCollection item2 = new() { Discharges = new List<Discharge> { new() { Timestamp = DateTime.Now.AddDays(-10) }, new() { Timestamp = DateTime.Now.AddDays(-1) } } };
            DischargeCollection item3 = new() { Discharges = new List<Discharge> { new() { Timestamp = DateTime.Now.AddDays(-5) }, new() { Timestamp = DateTime.Now.AddDays(-2) } } };

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DischargeCollection>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(new List<DischargeCollection> { item1, item2, item3 });

            DischargeContext dischargeContext = new(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            IEnumerable<DischargeCollection> subject = dischargeContext.Get();

            subject.Should().ContainInOrder(item2, item3, item1);
        }
    }
}
