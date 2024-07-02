using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Modpack;

public class BuildsDataServiceTests
{
    private readonly BuildsContext _buildsContext;
    private readonly Mock<Api.Core.Context.Base.IMongoCollection<ModpackBuild>> _mockDataCollection;
    private readonly Mock<IEventBus> _mockEventBus;

    public BuildsDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockEventBus = new Mock<IEventBus>();
        _mockDataCollection = new Mock<Api.Core.Context.Base.IMongoCollection<ModpackBuild>>();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<ModpackBuild>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _buildsContext = new BuildsContext(mockDataCollectionFactory.Object, _mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        ModpackBuild item1 = new() { BuildNumber = 4 };
        ModpackBuild item2 = new() { BuildNumber = 10 };
        ModpackBuild item3 = new() { BuildNumber = 9 };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(
            new List<ModpackBuild>
            {
                item1,
                item2,
                item3
            }
        );

        var subject = _buildsContext.Get();

        subject.Should().ContainInOrder(item2, item3, item1);
    }

    [Fact]
    public async Task Should_update_build_step_with_event()
    {
        var id = ObjectId.GenerateNewId().ToString();
        ModpackBuildStep modpackBuildStep = new("step") { Index = 0, Running = false };
        ModpackBuild modpackBuild = new()
        {
            Id = id,
            BuildNumber = 1,
            Steps = new List<ModpackBuildStep> { modpackBuildStep }
        };
        EventModel subject = null;

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackBuild>());
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<ModpackBuild>>()))
                           .Callback(() => { modpackBuild.Steps.First().Running = true; });
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(x => subject = x);

        await _buildsContext.Update(modpackBuild, modpackBuildStep);

        modpackBuildStep.Running.Should().BeTrue();
        subject.Data.Should().NotBeNull();
        subject.Data.Should().BeOfType<ModpackBuildStepEventData>();
    }

    [Fact]
    public async Task Should_update_build_with_event_data()
    {
        var id = ObjectId.GenerateNewId().ToString();
        EventModel subject = null;

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackBuild>());
        _mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<ModpackBuild>>()));
        _mockEventBus.Setup(x => x.Send(It.IsAny<EventModel>())).Callback<EventModel>(x => subject = x);

        var modpackBuild = new ModpackBuild { Id = id, BuildNumber = 1 };
        var modpackBuildEventData = new ModpackBuildEventData(modpackBuild);
        await _buildsContext.Update(modpackBuild, Builders<ModpackBuild>.Update.Set(x => x.Running, true));

        subject.Data.Should().NotBeNull();
        subject.Data.Should().BeEquivalentTo(modpackBuildEventData);
    }
}
