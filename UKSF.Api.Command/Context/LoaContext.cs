using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Context {
    public interface ILoaContext : IMongoContext<Loa>, ICachedMongoContext { }

    public class LoaContext : CachedMongoContext<Loa>, ILoaContext {
        public LoaContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "loas") { }
    }
}
