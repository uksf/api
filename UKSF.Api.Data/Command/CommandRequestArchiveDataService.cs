using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Command;

namespace UKSF.Api.Data.Command {
    public class CommandRequestArchiveDataService : DataService<CommandRequest, ICommandRequestArchiveDataService>, ICommandRequestArchiveDataService {
        public CommandRequestArchiveDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ICommandRequestArchiveDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "commandRequestsArchive") { }
    }
}
