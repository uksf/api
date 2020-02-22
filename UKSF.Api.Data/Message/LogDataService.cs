using System.Threading.Tasks;
using UKSF.Api.Events;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message.Logging;
using UKSF.Common;

namespace UKSF.Api.Data.Message {
    public class LogDataService : DataService<BasicLogMessage, ILogDataService>, ILogDataService {
        private readonly IDataCollection dataCollection;

        public LogDataService(IDataCollection dataCollection, IDataEventBus<ILogDataService> dataEventBus) : base(dataCollection, dataEventBus, "logs") => this.dataCollection = dataCollection;

        public async Task Add(AuditLogMessage log) {
            await dataCollection.Add("auditLogs", log);
            DataEvent(EventModelFactory.CreateDataEvent<ILogDataService>(DataEventType.ADD, log.id, log));
        }

        public async Task Add(LauncherLogMessage log) {
            await dataCollection.Add("launcherLogs", log);
            DataEvent(EventModelFactory.CreateDataEvent<ILogDataService>(DataEventType.ADD, log.id, log));
        }

        public async Task Add(WebLogMessage log) {
            await dataCollection.Add("errorLogs", log);
            DataEvent(EventModelFactory.CreateDataEvent<ILogDataService>(DataEventType.ADD, log.id, log));
        }
    }
}
