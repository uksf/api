using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Command.Context
{
    public interface IOperationReportContext : IMongoContext<Oprep>, ICachedMongoContext { }

    public class OperationReportContext : CachedMongoContext<Oprep>, IOperationReportContext
    {
        public OperationReportContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "oprep") { }

        protected override void SetCache(IEnumerable<Oprep> newCollection)
        {
            lock (LockObject)
            {
                Cache = newCollection?.OrderBy(x => x.Start).ToList();
            }
        }
    }
}
