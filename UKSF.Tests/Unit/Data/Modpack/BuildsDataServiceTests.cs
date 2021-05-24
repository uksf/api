using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Modpack
{
    public class BuildsDataServiceTests
    {
        private readonly BuildsContext _buildsContext;
        private readonly Mock<Api.Base.Context.IMongoCollection<ModpackBuild>> _mockDataCollection;
        private readonly Mock<IEventBus> _mockEventBus;

        public BuildsDataServiceTests()
        {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            _mockEventBus = new();
            _mockDataCollection = new();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<ModpackBuild>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

            _buildsContext = new(mockDataCollectionFactory.Object, _mockEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order()
        {
            ModpackBuild item1 = new() { BuildNumber = 4 };
            ModpackBuild item2 = new() { BuildNumber = 10 };
            ModpackBuild item3 = new() { BuildNumber = 9 };

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackBuild> { item1, item2, item3 });

            IEnumerable<ModpackBuild> subject = _buildsContext.Get();

            subject.Should().ContainInOrder(item2, item3, item1);
        }

        [Fact]
        public void Should_update_build_step_with_event()
        {
            string id = ObjectId.GenerateNewId().ToString();
            ModpackBuildStep modpackBuildStep = new("step") { Index = 0, Running = false };
            ModpackBuild modpackBuild = new() { Id = id, BuildNumber = 1, Steps = new() { modpackBuildStep } };
            EventModel subject = null;

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackBuild>());
            _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<ModpackBuild>>())).Callback(() => { modpackBuild.Steps.First().Running = true; });
            _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(x => subject = x);

            _buildsContext.Update(modpackBuild, modpackBuildStep);

            modpackBuildStep.Running.Should().BeTrue();
            subject.Data.Should().NotBeNull();
            subject.Data.Should().BeOfType<ModpackBuildStepEventData>();
        }

        [Fact]
        public void Should_update_build_with_event_data()
        {
            string id = ObjectId.GenerateNewId().ToString();
            EventModel subject = null;

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackBuild>());
            _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<ModpackBuild>>()));
            _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(x => subject = x);

            ModpackBuild modpackBuild = new() { Id = id, BuildNumber = 1 };
            _buildsContext.Update(modpackBuild, Builders<ModpackBuild>.Update.Set(x => x.Running, true));

            subject.Data.Should().NotBeNull();
            subject.Data.Should().Be(modpackBuild);
        }
    }
}
