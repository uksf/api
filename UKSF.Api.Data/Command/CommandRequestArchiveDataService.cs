using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Command;

namespace UKSF.Api.Data.Command {
    public class CommandRequestArchiveDataService : DataService<CommandRequest, ICommandRequestArchiveDataService>, ICommandRequestArchiveDataService {
        public CommandRequestArchiveDataService(IMongoDatabase database, IDataEventBus<ICommandRequestArchiveDataService> dataEventBus) : base(database, dataEventBus, "commandRequestsArchive") { }
    }
}
