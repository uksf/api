using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class EventModelFactoryTests {
        [Fact]
        public void Should_create_data_event_correctly() {
            string id = ObjectId.GenerateNewId().ToString();
            object data = new[] { "test", "item" };

            DataEventModel<TestDataModel> subject = EventModelFactory.CreateDataEvent<TestDataModel>(DataEventType.ADD, id, data);

            subject.Should().NotBeNull();
            subject.Type.Should().Be(DataEventType.ADD);
            subject.Id.Should().Be(id);
            subject.Data.Should().Be(data);
        }
    }
}
