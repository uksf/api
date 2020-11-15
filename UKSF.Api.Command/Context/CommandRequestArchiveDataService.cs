using UKSF.Api.Base.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

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
