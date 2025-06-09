using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Context;

public interface IReleasesContext : IMongoContext<DomainModpackRelease>, ICachedMongoContext;

public class ReleasesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainModpackRelease>(mongoCollectionFactory, eventBus, variablesService, "modpackReleases"), IReleasesContext
{
    protected override IEnumerable<DomainModpackRelease> OrderCollection(IEnumerable<DomainModpackRelease> collection)
    {
        return collection.Select(x =>
                             {
                                 var parts = x.Version.Split('.').Select(int.Parse).ToArray();
                                 return new
                                 {
                                     release = x,
                                     major = parts[0],
                                     minor = parts[1],
                                     patch = parts[2]
                                 };
                             }
                         )
                         .OrderByDescending(x => x.major)
                         .ThenByDescending(x => x.minor)
                         .ThenByDescending(x => x.patch)
                         .Select(x => x.release);
    }
}
