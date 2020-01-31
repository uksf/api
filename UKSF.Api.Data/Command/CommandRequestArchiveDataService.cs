using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Command;

namespace UKSF.Api.Data.Command {
    public class CommandRequestArchiveDataService : DataService<CommandRequest, ICommandRequestArchiveDataService>, ICommandRequestArchiveDataService {
        public CommandRequestArchiveDataService(IDataCollection dataCollection, IDataEventBus<ICommandRequestArchiveDataService> dataEventBus) : base(dataCollection, dataEventBus, "commandRequestsArchive") { }
    }
}
