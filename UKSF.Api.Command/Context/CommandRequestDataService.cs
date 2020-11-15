using UKSF.Api.Base.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Context {
    public interface ICommandRequestDataService : IDataService<CommandRequest>, ICachedDataService { }

    public class CommandRequestDataService : CachedDataService<CommandRequest>, ICommandRequestDataService {
        public CommandRequestDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<CommandRequest> dataEventBus) : base(dataCollectionFactory, dataEventBus, "commandRequests") { }
    }
}
