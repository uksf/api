using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Data.Command {
    public class CommandRequestArchiveDataService : DataService<CommandRequest>, ICommandRequestArchiveDataService {
        public CommandRequestArchiveDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<CommandRequest> dataEventBus) : base(
            dataCollectionFactory,
            dataEventBus,
            "commandRequestsArchive"
        ) { }

        protected override void DataEvent(DataEventModel<CommandRequest> dataEvent) { }
    }
}
