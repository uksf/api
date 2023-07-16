using MongoDB.Driver;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Context;

public interface IBuildsContext : IMongoContext<ModpackBuild>, ICachedMongoContext
{
    Task Update(ModpackBuild build, ModpackBuildStep buildStep);
    Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition);
}

public class BuildsContext : CachedMongoContext<ModpackBuild>, IBuildsContext
{
    public BuildsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "modpackBuilds"
    ) { }

    public async Task Update(ModpackBuild build, ModpackBuildStep buildStep)
    {
        var updateDefinition = Builders<ModpackBuild>.Update.Set(x => x.Steps[buildStep.Index], buildStep);
        await base.Update(build.Id, updateDefinition);
        DataEvent(new(EventType.UPDATE, new ModpackBuildStepEventData(build.Id, buildStep)));
    }

    public async Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition)
    {
        await base.Update(build.Id, updateDefinition);
        DataEvent(new(EventType.UPDATE, build));
    }

    public override IEnumerable<ModpackBuild> Get()
    {
        return base.Get().OrderByDescending(x => x.BuildNumber);
    }

    public override IEnumerable<ModpackBuild> Get(Func<ModpackBuild, bool> predicate)
    {
        return base.Get(predicate).OrderByDescending(x => x.BuildNumber);
    }
}
