using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Command;

namespace UKSFWebsite.Api.Data.Command {
    public class CommandRequestArchiveDataService : DataService<CommandRequest, ICommandRequestArchiveDataService>, ICommandRequestArchiveDataService {
        public CommandRequestArchiveDataService(IMongoDatabase database, IDataEventBus<ICommandRequestArchiveDataService> dataEventBus) : base(database, dataEventBus, "commandRequestsArchive") { }
    }
}
