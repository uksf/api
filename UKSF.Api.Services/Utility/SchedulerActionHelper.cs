using System;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Utility {
    public static class SchedulerActionHelper {
        private const ulong ID_CHANNEL_GENERAL = 311547576942067713;

        public static void DeleteExpiredConfirmationCode(string id) {
            ServiceWrapper.ServiceProvider.GetService<IConfirmationCodeService>().Data.Delete(id);
        }

        public static void PruneLogs() {
            DateTime now = DateTime.Now;
            IMongoDatabase database = ServiceWrapper.ServiceProvider.GetService<IMongoDatabase>();
            database.GetCollection<BasicLogMessage>("logs").DeleteManyAsync(message => message.timestamp < now.AddDays(-7));
            database.GetCollection<BasicLogMessage>("errorLogs").DeleteManyAsync(message => message.timestamp < now.AddDays(-7));
            database.GetCollection<BasicLogMessage>("auditLogs").DeleteManyAsync(message => message.timestamp < now.AddMonths(-1));
            database.GetCollection<Notification>("notifications").DeleteManyAsync(message => message.timestamp < now.AddMonths(-1));
        }

        public static void TeamspeakSnapshot() {
            ServiceWrapper.ServiceProvider.GetService<ITeamspeakService>().StoreTeamspeakServerSnapshot();
        }
    }
}
