using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Data.Modpack;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Modpack;
using Xunit;

namespace UKSF.Tests.Unit.Data.Modpack {
    public class BuildsDataServiceTests {
        private readonly BuildsDataService buildsDataService;
        private readonly Mock<IDataCollection<ModpackBuild>> mockDataCollection;
        private readonly Mock<IDataEventBus<ModpackBuild>> mockDataEventBus;

        public BuildsDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockDataEventBus = new Mock<IDataEventBus<ModpackBuild>>();
            mockDataCollection = new Mock<IDataCollection<ModpackBuild>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<ModpackBuild>(It.IsAny<string>())).Returns(mockDataCollection.Object);

            buildsDataService = new BuildsDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order() {
            ModpackBuild item1 = new ModpackBuild { buildNumber = 4 };
            ModpackBuild item2 = new ModpackBuild { buildNumber = 10 };
            ModpackBuild item3 = new ModpackBuild { buildNumber = 9 };

            mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackBuild> { item1, item2, item3 });

            IEnumerable<ModpackBuild> subject = buildsDataService.Get();

            subject.Should().ContainInOrder(item2, item3, item1);
        }

        [Fact]
        public void Should_update_build_step_with_event() {
            string id = ObjectId.GenerateNewId().ToString();
            ModpackBuildStep modpackBuildStep = new ModpackBuildStep("step") { index = 0, running = false };
            ModpackBuild modpackBuild = new ModpackBuild { id = id, buildNumber = 1, steps = new List<ModpackBuildStep> { modpackBuildStep } };
            DataEventModel<ModpackBuild> subject = null;

            mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackBuild>());
            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<ModpackBuild>>()))
                              .Callback(() => { modpackBuild.steps.First().running = true; });
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<ModpackBuild>>())).Callback<DataEventModel<ModpackBuild>>(x => subject = x);

            buildsDataService.Update(modpackBuild, modpackBuildStep);

            modpackBuildStep.running.Should().BeTrue();
            subject.data.Should().NotBeNull();
            subject.data.Should().Be(modpackBuildStep);
        }

        [Fact]
        public void Should_update_build_with_event_data() {
            string id = ObjectId.GenerateNewId().ToString();
            DataEventModel<ModpackBuild> subject = null;

            mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackBuild>());
            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<ModpackBuild>>()));
            mockDataEventBus.Setup(x => x.Send(It.IsAny<DataEventModel<ModpackBuild>>())).Callback<DataEventModel<ModpackBuild>>(x => subject = x);

            ModpackBuild modpackBuild = new ModpackBuild { id = id, buildNumber = 1 };
            buildsDataService.Update(modpackBuild, Builders<ModpackBuild>.Update.Set(x => x.running, true));

            subject.data.Should().NotBeNull();
            subject.data.Should().Be(modpackBuild);
        }
    }
}
