using System;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Events.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Tests.Unit.Data;
using Xunit;

namespace UKSF.Tests.Unit.Events {
    public class DataEventBackerTests {
        public DataEventBackerTests() {
            mockDataCollection = new Mock<IDataCollection>();
            dataEventBus = new DataEventBus<IMockDataService>();
        }

        private readonly Mock<IDataCollection> mockDataCollection;
        private readonly IDataEventBus<IMockDataService> dataEventBus;

        [Fact]
        public void ShouldReturnEventBus() {
            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, dataEventBus, "test");
            IObservable<DataEventModel<IMockDataService>> subject = mockDataService.EventBus();

            subject.Should().NotBeNull();
        }

        [Fact]
        public void ShouldSendEvent() {
            MockDataModel item1 = new MockDataModel {Name = "1"};
            string id = item1.id;

            DataEventModel<IMockDataService> subject = null;
            MockDataService mockDataService = new MockDataService(mockDataCollection.Object, dataEventBus, "test");
            mockDataService.EventBus().Subscribe(x => { subject = x; });
            mockDataService.Add(item1);

            subject.Should().NotBeNull();
            subject.id.Should().Be(id);
            subject.type.Should().Be(DataEventType.ADD);
            subject.data.Should().Be(item1);
        }
    }
}
