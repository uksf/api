using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Command;

namespace UKSF.Api.Data.Command {
    public class CommandRequestDataService : CachedDataService<CommandRequest, ICommandRequestDataService>, ICommandRequestDataService {
        public CommandRequestDataService(IDataCollection dataCollection, IDataEventBus<ICommandRequestDataService> dataEventBus) : base(dataCollection, dataEventBus, "commandRequests") { }
    }
}
