using System;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility.ScheduledActions;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Message.Logging;

namespace UKSF.Api.Services.Utility.ScheduledActions {
    public class PruneLogsAction : IPruneLogsAction {
        public const string ACTION_NAME = nameof(PruneLogsAction);

        private readonly IDataCollectionFactory dataCollectionFactory;

        public PruneLogsAction(IDataCollectionFactory dataCollectionFactory) => this.dataCollectionFactory = dataCollectionFactory;

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            DateTime now = DateTime.Now;
            Task logsTask = dataCollectionFactory.CreateDataCollection<BasicLogMessage>("logs").DeleteManyAsync(x => x.timestamp < now.AddDays(-7));
            Task errorLogsTask = dataCollectionFactory.CreateDataCollection<WebLogMessage>("errorLogs").DeleteManyAsync(x => x.timestamp < now.AddDays(-7));
            Task auditLogsTask = dataCollectionFactory.CreateDataCollection<AuditLogMessage>("auditLogs").DeleteManyAsync(x => x.timestamp < now.AddMonths(-3));
            Task notificationsTask = dataCollectionFactory.CreateDataCollection<Notification>("notifications").DeleteManyAsync(x => x.timestamp < now.AddMonths(-1));

            Task.WaitAll(logsTask, errorLogsTask, auditLogsTask, notificationsTask);
        }
    }
}
