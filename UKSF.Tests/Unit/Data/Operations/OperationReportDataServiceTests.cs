using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Operations;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Data.Operations {
    public class OperationReportDataServiceTests {
        private readonly Mock<IDataCollection<Oprep>> mockDataCollection;
        private readonly OperationReportDataService operationReportDataService;

        public OperationReportDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<IOperationReportDataService>> mockDataEventBus = new Mock<IDataEventBus<IOperationReportDataService>>();
            mockDataCollection = new Mock<IDataCollection<Oprep>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Oprep>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            operationReportDataService = new OperationReportDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void ShouldGetReversedCollection() {
            Oprep item1 = new Oprep();
            Oprep item2 = new Oprep();
            Oprep item3 = new Oprep();

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Oprep> {item1, item2, item3});

            List<Oprep> subject = operationReportDataService.Get();

            subject.Should().ContainInOrder(item3, item2, item1);
        }

        [Fact]
        public void ShouldGetReversedCollectionByPredicate() {
            Oprep item1 = new Oprep { description = "1" };
            Oprep item2 = new Oprep { description = "2" };
            Oprep item3 = new Oprep { description = "3" };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<Oprep> { item1, item2, item3 });

            List<Oprep> subject = operationReportDataService.Get(x => x.description != string.Empty);

            subject.Should().ContainInOrder(item3, item2, item1);
        }
    }
}
