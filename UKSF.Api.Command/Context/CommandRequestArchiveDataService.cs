using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Command.Models;

namespace UKSF.Api.Command.Context {
    public interface ICommandRequestArchiveDataService : IDataService<CommandRequest> { }

    public class CommandRequestArchiveDataService : DataService<CommandRequest>, ICommandRequestArchiveDataService {
        public CommandRequestArchiveDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<CommandRequest> dataEventBus) : base(
            dataCollectionFactory,
            dataEventBus,
            "commandRequestsArchive"
        ) { }

        protected override void DataEvent(DataEventModel<CommandRequest> dataEvent) { }
    }
}
