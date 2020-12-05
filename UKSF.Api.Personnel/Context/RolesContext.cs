using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface IRolesContext : IMongoContext<Role>, ICachedMongoContext {
        new IEnumerable<Role> Get();
        new Role GetSingle(string name);
    }

    public class RolesContext : CachedMongoContext<Role>, IRolesContext {
        public RolesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "roles") { }

        public override Role GetSingle(string name) => GetSingle(x => x.Name == name);

        protected override void SetCache(IEnumerable<Role> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Name).ToList();
            }
        }
    }
}
