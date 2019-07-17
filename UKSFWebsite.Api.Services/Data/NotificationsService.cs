using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Data {
    public static class NotificationIcons {
        public const string APPLICATION = "group_add";
        public const string COMMENT = "comment";
        public const string DEMOTION = "mood_bad";
        public const string PROMOTION = "stars";
        public const string REQUEST = "add_circle";
    }

    public class NotificationsService : CachedDataService<Notification>, INotificationsService {
        private readonly IAccountService accountService;
        private readonly IEmailService emailService;
        private readonly IHubContext<NotificationHub, INotificationsClient> notificationsHub;
        private readonly ISessionService sessionService;
        private readonly ITeamspeakService teamspeakService;

        public NotificationsService(ITeamspeakService teamspeakService, IAccountService accountService, ISessionService sessionService, IMongoDatabase database, IEmailService emailService, IHubContext<NotificationHub, INotificationsClient> notificationsHub) : base(
            database,
            "notifications"
        ) {
            this.teamspeakService = teamspeakService;
            this.accountService = accountService;
            this.sessionService = sessionService;
            this.emailService = emailService;
            this.notificationsHub = notificationsHub;
        }

        public void SendTeamspeakNotification(Account account, string rawMessage) {
            rawMessage = rawMessage.Replace("<a href='", "[url]").Replace("'>", "[/url]");
            teamspeakService.SendTeamspeakMessageToClient(account, rawMessage);
        }

        public void SendTeamspeakNotification(IEnumerable<string> clientDbIds, string rawMessage) {
            rawMessage = rawMessage.Replace("<a href='", "[url]").Replace("'>", "[/url]");
            teamspeakService.SendTeamspeakMessageToClient(clientDbIds, rawMessage);
        }

        public IEnumerable<Notification> GetNotificationsForContext() {
            string contextId = sessionService.GetContextId();
            return Get(x => x.owner == contextId);
        }

        public new void Add(Notification notification) {
            Task unused = AddNotificationAsync(notification);
        }

        public async Task MarkNotificationsAsRead(IEnumerable<string> ids) {
            ids = ids.ToList();
            string contextId = sessionService.GetContextId();
            FilterDefinition<Notification> filter = Builders<Notification>.Filter.Eq(x => x.owner, contextId) & Builders<Notification>.Filter.In(x => x.id, ids);
            await Database.GetCollection<Notification>(DatabaseCollection).UpdateManyAsync(filter, Builders<Notification>.Update.Set(x => x.read, true));
            Refresh();
            await notificationsHub.Clients.Group(contextId).ReceiveRead(ids);
        }

        public async Task Delete(IEnumerable<string> ids) {
            ids = ids.ToList();
            string contextId = sessionService.GetContextId();
            await Database.GetCollection<Notification>(DatabaseCollection).DeleteManyAsync(Builders<Notification>.Filter.Eq(x => x.owner, contextId) & Builders<Notification>.Filter.In(x => x.id, ids));
            Refresh();
            await notificationsHub.Clients.Group(contextId).ReceiveClear(ids);
        }

        private async Task AddNotificationAsync(Notification notification) {
            notification.message = notification.message.ConvertObjectIds();
            await base.Add(notification);
            Account account = accountService.GetSingle(notification.owner);
            if (account.settings.notificationsEmail) {
                SendEmailNotification(account.email, $"{notification.message}{(notification.link != null ? $"<br><a href='https://uk-sf.co.uk{notification.link}'>https://uk-sf.co.uk{notification.link}</a>" : "")}");
            }

            if (account.settings.notificationsTeamspeak) {
                SendTeamspeakNotification(account, $"{notification.message}{(notification.link != null ? $"\n[url]https://uk-sf.co.uk{notification.link}[/url]" : "")}");
            }

            await notificationsHub.Clients.Group(account.id).ReceiveNotification(notification);
        }

        private void SendEmailNotification(string email, string message) {
            message += "<br><br><sub>You can opt-out of these emails by unchecking 'Email notifications' in your <a href='https://uk-sf.co.uk/profile'>Profile</a></sub>";
            emailService.SendEmail(email, "UKSF Notification", message);
        }
    }
}
