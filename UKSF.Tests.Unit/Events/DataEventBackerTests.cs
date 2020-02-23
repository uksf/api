using System;
using FluentAssertions;
using Moq;
using UKSF.Api.Data;
using UKSF.Api.Events.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Tests.Unit.Data;
using Xunit;

namespace UKSF.Tests.Unit.Events {
    public class DataEventBackerTests {
        private readonly MockDataService mockDataService;

        public DataEventBackerTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            IDataEventBus<IMockDataService> dataEventBus = new DataEventBus<IMockDataService>();
            Mock<IDataCollection> mockDataCollection = new Mock<IDataCollection>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection(It.IsAny<string>())).Returns(mockDataCollection.Object);

            mockDataService = new MockDataService(mockDataCollectionFactory.Object, dataEventBus, "test");
        }

        [Fact]
        public void ShouldReturnEventBus() {
            IObservable<DataEventModel<IMockDataService>> subject = mockDataService.EventBus();

            subject.Should().NotBeNull();
        }

        [Fact]
        public void ShouldSendEvent() {
            MockDataModel item1 = new MockDataModel {Name = "1"};
            string id = item1.id;

            DataEventModel<IMockDataService> subject = null;
            mockDataService.EventBus().Subscribe(x => { subject = x; });
            mockDataService.Add(item1);

            subject.Should().NotBeNull();
            subject.id.Should().Be(id);
            subject.type.Should().Be(DataEventType.ADD);
            subject.data.Should().Be(item1);
        }
    }
}
