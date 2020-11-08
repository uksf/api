using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class EventModelFactoryTests {
        [Fact]
        public void Should_create_data_event_correctly() {
            string id = ObjectId.GenerateNewId().ToString();
            object data = new[] { "test", "item" };

            DataEventModel<TestDataModel> subject = EventModelFactory.CreateDataEvent<TestDataModel>(DataEventType.ADD, id, data);

            subject.Should().NotBeNull();
            subject.type.Should().Be(DataEventType.ADD);
            subject.id.Should().Be(id);
            subject.data.Should().Be(data);
        }
    }
}
