using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Context;

public interface IReleasesContext : IMongoContext<ModpackRelease>, ICachedMongoContext { }

public class ReleasesContext : CachedMongoContext<ModpackRelease>, IReleasesContext
{
    public ReleasesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "modpackReleases"
    ) { }

    protected override IEnumerable<ModpackRelease> OrderCollection(IEnumerable<ModpackRelease> collection)
    {
        return collection.Select(
                             x =>
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
