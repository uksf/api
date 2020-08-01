using System;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility.ScheduledActions;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Utility.ScheduledActions {
    public class PruneDataAction : IPruneDataAction {
        public const string ACTION_NAME = nameof(PruneDataAction);

        private readonly IDataCollectionFactory dataCollectionFactory;

        public PruneDataAction(IDataCollectionFactory dataCollectionFactory) => this.dataCollectionFactory = dataCollectionFactory;

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            DateTime now = DateTime.Now;
            Task logsTask = dataCollectionFactory.CreateDataCollection<BasicLogMessage>("logs").DeleteManyAsync(x => x.timestamp < now.AddDays(-7));
            Task errorLogsTask = dataCollectionFactory.CreateDataCollection<WebLogMessage>("errorLogs").DeleteManyAsync(x => x.timestamp < now.AddDays(-7));
            Task auditLogsTask = dataCollectionFactory.CreateDataCollection<AuditLogMessage>("auditLogs").DeleteManyAsync(x => x.timestamp < now.AddMonths(-3));
            Task notificationsTask = dataCollectionFactory.CreateDataCollection<Notification>("notifications").DeleteManyAsync(x => x.timestamp < now.AddMonths(-1));

            IDataCollection<ModpackBuild> buildsData = dataCollectionFactory.CreateDataCollection<ModpackBuild>("modpackBuilds");
            int threshold = buildsData.Get(x => x.environment == GameEnvironment.DEV).Select(x => x.buildNumber).OrderByDescending(x => x).First() - 100;
            Task modpackBuildsTask = buildsData.DeleteManyAsync(x => x.buildNumber < threshold);

            Task.WaitAll(logsTask, errorLogsTask, auditLogsTask, notificationsTask, modpackBuildsTask);
        }
    }
}
