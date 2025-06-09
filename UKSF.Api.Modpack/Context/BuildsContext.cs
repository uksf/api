using MongoDB.Driver;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Context;

public interface IBuildsContext : IMongoContext<DomainModpackBuild>, ICachedMongoContext
{
    Task Update(DomainModpackBuild build, ModpackBuildStep buildStep);
    Task Update(DomainModpackBuild build, UpdateDefinition<DomainModpackBuild> updateDefinition);
}

public class BuildsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainModpackBuild>(mongoCollectionFactory, eventBus, variablesService, "modpackBuilds"), IBuildsContext
{
    public async Task Update(DomainModpackBuild build, ModpackBuildStep buildStep)
    {
        var updateDefinition = Builders<DomainModpackBuild>.Update.Set(x => x.Steps[buildStep.Index], buildStep);
        await base.Update(build.Id, updateDefinition);
        DataEvent(EventType.Update, new ModpackBuildStepEventData(build.Id, buildStep));
    }

    public async Task Update(DomainModpackBuild build, UpdateDefinition<DomainModpackBuild> updateDefinition)
    {
        await base.Update(build.Id, updateDefinition);
        DataEvent(EventType.Update, new ModpackBuildEventData(build));
    }

    protected override IEnumerable<DomainModpackBuild> OrderCollection(IEnumerable<DomainModpackBuild> collection)
    {
        return collection.OrderByDescending(x => x.BuildNumber);
    }
}
