using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Modpack.Context
{
    public interface IReleasesContext : IMongoContext<ModpackRelease>, ICachedMongoContext { }

    public class ReleasesContext : CachedMongoContext<ModpackRelease>, IReleasesContext
    {
        public ReleasesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "modpackReleases") { }

        protected override void SetCache(IEnumerable<ModpackRelease> newCollection)
        {
            lock (LockObject)
            {
                Cache = newCollection?.Select(
                                         x =>
                                         {
                                             var parts = x.Version.Split('.').Select(int.Parse).ToArray();
                                             return new { release = x, major = parts[0], minor = parts[1], patch = parts[2] };
                                         }
                                     )
                                     .OrderByDescending(x => x.major)
                                     .ThenByDescending(x => x.minor)
                                     .ThenByDescending(x => x.patch)
                                     .Select(x => x.release)
                                     .ToList();
            }
        }
    }
}
