using System.Threading.Tasks;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Logging.Models;

namespace UKSF.Api.Logging.Services.Data {
    public interface ILogDataService : IDataService<BasicLogMessage> {
        Task Add(AuditLogMessage log);
        Task Add(LauncherLogMessage log);
        Task Add(WebLogMessage log);
    }

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
