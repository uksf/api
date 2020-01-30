using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message.Logging;

namespace UKSF.Api.Data.Message {
    public class LogDataService : DataService<BasicLogMessage, ILogDataService>, ILogDataService {
        private readonly IMongoDatabase database;

        public LogDataService(IMongoDatabase database, IDataEventBus<ILogDataService> dataEventBus) : base(database, dataEventBus, "logs") => this.database = database;

        public override async Task Add(BasicLogMessage log) {
            await base.Add(log);
            DataEvent(EventModelFactory.CreateDataEvent<ILogDataService>(DataEventType.ADD, GetIdValue(log), log));
        }

        public async Task Add(AuditLogMessage log) {
            await database.GetCollection<AuditLogMessage>("auditLogs").InsertOneAsync(log);
            DataEvent(EventModelFactory.CreateDataEvent<ILogDataService>(DataEventType.ADD, GetIdValue(log), log));
        }

        public async Task Add(LauncherLogMessage log) {
            await database.GetCollection<LauncherLogMessage>("launcherLogs").InsertOneAsync(log);
            DataEvent(EventModelFactory.CreateDataEvent<ILogDataService>(DataEventType.ADD, GetIdValue(log), log));
        }

        public async Task Add(WebLogMessage log) {
            await database.GetCollection<WebLogMessage>("errorLogs").InsertOneAsync(log);
            DataEvent(EventModelFactory.CreateDataEvent<ILogDataService>(DataEventType.ADD, GetIdValue(log), log));
        }
    }
}
