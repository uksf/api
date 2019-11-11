using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Command;

namespace UKSFWebsite.Api.Data.Command {
    public class CommandRequestDataService : CachedDataService<CommandRequest>, ICommandRequestDataService {
        public CommandRequestDataService(IMongoDatabase database, IEventBus dataEventBus) : base(database, dataEventBus, "commandRequests") { }
    }
}
