using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Operations;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;
using Xunit;

namespace UKSF.Tests.Unit.Data.Operations {
    public class OperationOrderDataServiceTests {
        private readonly Mock<IDataCollection> mockDataCollection;
        private readonly OperationOrderDataService operationOrderDataService;

        public OperationOrderDataServiceTests() {
            mockDataCollection = new Mock<IDataCollection>();
            Mock<IDataEventBus<IOperationOrderDataService>> mockDataEventBus = new Mock<IDataEventBus<IOperationOrderDataService>>();

            operationOrderDataService = new OperationOrderDataService(mockDataCollection.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void ShouldGetReversedCollection() {
            Opord item1 = new Opord();
            Opord item2 = new Opord();
            Opord item3 = new Opord();

            mockDataCollection.Setup(x => x.Get<Opord>()).Returns(new List<Opord> { item1, item2, item3 });

            List<Opord> subject = operationOrderDataService.Get();

            subject.Should().ContainInOrder(item3, item2, item1);
        }

        [Fact]
        public void ShouldGetReversedCollectionByPredicate() {
            Opord item1 = new Opord { description = "1" };
            Opord item2 = new Opord { description = "2" };
            Opord item3 = new Opord { description = "3" };

            mockDataCollection.Setup(x => x.Get<Opord>()).Returns(new List<Opord> { item1, item2, item3 });

            List<Opord> subject = operationOrderDataService.Get(x => x.description != string.Empty);

            subject.Should().ContainInOrder(item3, item2, item1);
        }
    }
}
