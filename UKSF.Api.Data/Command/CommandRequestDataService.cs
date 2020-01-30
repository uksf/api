using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Command;

namespace UKSF.Api.Data.Command {
    public class CommandRequestDataService : CachedDataService<CommandRequest, ICommandRequestDataService>, ICommandRequestDataService {
        public CommandRequestDataService(IMongoDatabase database, IDataEventBus<ICommandRequestDataService> dataEventBus) : base(database, dataEventBus, "commandRequests") { }
    }
}
