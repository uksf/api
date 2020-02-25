using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Events.Types;
using UKSF.Common;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class EventModelFactoryTests {
        [Fact]
        public void ShouldReturnDataEvent() {
            string id = ObjectId.GenerateNewId().ToString();
            object data = new[] {"test", "item"};

            DataEventModel<MockDataModel> subject = EventModelFactory.CreateDataEvent<MockDataModel>(DataEventType.ADD, id, data);

            subject.Should().NotBeNull();
            subject.type.Should().Be(DataEventType.ADD);
            subject.id.Should().Be(id);
            subject.data.Should().Be(data);
        }

        [Fact]
        public void ShouldReturnSignalrEvent() {
            object args = new[] {"test", "item"};

            SignalrEventModel subject = EventModelFactory.CreateSignalrEvent(TeamspeakEventType.CLIENTS, args);

            subject.Should().NotBeNull();
            subject.procedure.Should().Be(TeamspeakEventType.CLIENTS);
            subject.args.Should().Be(args);
        }
    }
}
