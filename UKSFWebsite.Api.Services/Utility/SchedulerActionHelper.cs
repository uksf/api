using System;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Logging;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;

namespace UKSFWebsite.Api.Services.Utility {
    public static class SchedulerActionHelper {
        private const ulong ID_CHANNEL_GENERAL = 311547576942067713;

        public static void DeleteExpiredConfirmationCode(string id) {
            ServiceWrapper.ServiceProvider.GetService<IConfirmationCodeService>().Delete(id);
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

        public static void DiscordVoteAnnouncement() {
            bool run = bool.Parse(VariablesWrapper.VariablesService().GetSingle("RUN_DISCORD_CLANLIST").AsString());
            if (!run) return;
            ServiceWrapper.ServiceProvider.GetService<IDiscordService>().SendMessage(ID_CHANNEL_GENERAL, "@everyone - As part of our recruitment drive, we're aiming to gain exposure through a high ranking on Clanlist. To help with this, please go to https://clanlist.io/vote/UKSFMilsim and vote");
        }
    }
}
