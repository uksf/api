using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Operations {
    public class OperationOrderDataServiceTests {
        private readonly Mock<IDataCollection<Opord>> mockDataCollection;
        private readonly OperationOrderDataService operationOrderDataService;

        public OperationOrderDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<Opord>> mockDataEventBus = new Mock<IDataEventBus<Opord>>();
            mockDataCollection = new Mock<IDataCollection<Opord>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Opord>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            operationOrderDataService = new OperationOrderDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order() {
            Opord item1 = new Opord { Start = DateTime.Now.AddDays(-1) };
            Opord item2 = new Opord { Start = DateTime.Now.AddDays(-2) };
            Opord item3 = new Opord { Start = DateTime.Now.AddDays(-3) };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Opord> { item1, item2, item3 });

            IEnumerable<Opord> subject = operationOrderDataService.Get();

            subject.Should().ContainInOrder(item3, item2, item1);
        }

        [Fact]
        public void ShouldGetOrderedCollectionByPredicate() {
            Opord item1 = new Opord { Description = "1", Start = DateTime.Now.AddDays(-1) };
            Opord item2 = new Opord { Description = "2", Start = DateTime.Now.AddDays(-2) };
            Opord item3 = new Opord { Description = "1", Start = DateTime.Now.AddDays(-3) };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Opord> { item1, item2, item3 });

            IEnumerable<Opord> subject = operationOrderDataService.Get(x => x.Description == "1");

            subject.Should().ContainInOrder(item3, item1);
        }
    }
}
