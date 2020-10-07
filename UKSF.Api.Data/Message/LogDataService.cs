using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message.Logging;
using UKSF.Common;

namespace UKSF.Api.Data.Message {
    public class LogDataService : DataService<BasicLogMessage>, ILogDataService {
        private readonly IDataCollectionFactory dataCollectionFactory;

        public LogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<BasicLogMessage> dataEventBus) : base(dataCollectionFactory, dataEventBus, "logs") => this.dataCollectionFactory = dataCollectionFactory;

        public async Task Add(AuditLogMessage log) {
            await dataCollectionFactory.CreateDataCollection<AuditLogMessage>("auditLogs").AddAsync(log);
            DataEvent(EventModelFactory.CreateDataEvent<BasicLogMessage>(DataEventType.ADD, log.id, log));
        }

        public async Task Add(LauncherLogMessage log) {
            await dataCollectionFactory.CreateDataCollection<LauncherLogMessage>("launcherLogs").AddAsync(log);
            DataEvent(EventModelFactory.CreateDataEvent<BasicLogMessage>(DataEventType.ADD, log.id, log));
        }

        public async Task Add(WebLogMessage log) {
            await dataCollectionFactory.CreateDataCollection<WebLogMessage>("errorLogs").AddAsync(log);
            DataEvent(EventModelFactory.CreateDataEvent<BasicLogMessage>(DataEventType.ADD, log.id, log));
        }
    }
}
