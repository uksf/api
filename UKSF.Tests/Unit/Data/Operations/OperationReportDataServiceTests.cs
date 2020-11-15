using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data.Operations {
    public class OperationReportDataServiceTests {
        private readonly Mock<IDataCollection<Oprep>> mockDataCollection;
        private readonly OperationReportDataService operationReportDataService;

        public OperationReportDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<Oprep>> mockDataEventBus = new Mock<IDataEventBus<Oprep>>();
            mockDataCollection = new Mock<IDataCollection<Oprep>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Oprep>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            operationReportDataService = new OperationReportDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order() {
            Oprep item1 = new Oprep { Start = DateTime.Now.AddDays(-1) };
            Oprep item2 = new Oprep { Start = DateTime.Now.AddDays(-2) };
            Oprep item3 = new Oprep { Start = DateTime.Now.AddDays(-3) };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Oprep> { item1, item2, item3 });

            IEnumerable<Oprep> subject = operationReportDataService.Get();

            subject.Should().ContainInOrder(item3, item2, item1);
        }

        [Fact]
        public void ShouldGetOrderedCollectionByPredicate() {
            Oprep item1 = new Oprep { Description = "1", Start = DateTime.Now.AddDays(-1) };
            Oprep item2 = new Oprep { Description = "2", Start = DateTime.Now.AddDays(-2) };
            Oprep item3 = new Oprep { Description = "1", Start = DateTime.Now.AddDays(-3) };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Oprep> { item1, item2, item3 });

            IEnumerable<Oprep> subject = operationReportDataService.Get(x => x.Description == "1");

            subject.Should().ContainInOrder(item3, item1);
        }
    }
}
