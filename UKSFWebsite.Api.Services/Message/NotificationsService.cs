using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Message {
    public class NotificationsService : INotificationsService {
        private readonly IAccountService accountService;
        private readonly INotificationsDataService data;
        private readonly IEmailService emailService;
        private readonly IHubContext<NotificationHub, INotificationsClient> notificationsHub;
        private readonly ISessionService sessionService;
        private readonly ITeamspeakService teamspeakService;

        public NotificationsService(INotificationsDataService data, ITeamspeakService teamspeakService, IAccountService accountService, ISessionService sessionService, IEmailService emailService, IHubContext<NotificationHub, INotificationsClient> notificationsHub) {
            this.data = data;
            this.teamspeakService = teamspeakService;
            this.accountService = accountService;
            this.sessionService = sessionService;
            this.emailService = emailService;
            this.notificationsHub = notificationsHub;
        }

        public INotificationsDataService Data() => data;

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
            return data.Get(x => x.owner == contextId);
        }

        public void Add(Notification notification) {
            if (notification == null) return;
            Task unused = AddNotificationAsync(notification);
        }

        public async Task MarkNotificationsAsRead(IEnumerable<string> ids) {
            ids = ids.ToList();
            string contextId = sessionService.GetContextId();
            await data.UpdateMany(Builders<Notification>.Filter.Eq(x => x.owner, contextId) & Builders<Notification>.Filter.In(x => x.id, ids), Builders<Notification>.Update.Set(x => x.read, true));
            await notificationsHub.Clients.Group(contextId).ReceiveRead(ids);
        }

        public async Task Delete(IEnumerable<string> ids) {
            ids = ids.ToList();
            string contextId = sessionService.GetContextId();
            await data.DeleteMany(Builders<Notification>.Filter.Eq(x => x.owner, contextId) & Builders<Notification>.Filter.In(x => x.id, ids));
            await notificationsHub.Clients.Group(contextId).ReceiveClear(ids);
        }

        private async Task AddNotificationAsync(Notification notification) {
            notification.message = notification.message.ConvertObjectIds();
            await data.Add(notification);
            Account account = accountService.Data().GetSingle(notification.owner);
            if (account.settings.notificationsEmail) {
                SendEmailNotification(account.email, $"{notification.message}{(notification.link != null ? $"<br><a href='https://uk-sf.co.uk{notification.link}'>https://uk-sf.co.uk{notification.link}</a>" : "")}");
            }

            if (account.settings.notificationsTeamspeak) {
                SendTeamspeakNotification(account, $"{notification.message}{(notification.link != null ? $"\n[url]https://uk-sf.co.uk{notification.link}[/url]" : "")}");
            }
        }

        private void SendEmailNotification(string email, string message) {
            message += "<br><br><sub>You can opt-out of these emails by unchecking 'Email notifications' in your <a href='https://uk-sf.co.uk/profile'>Profile</a></sub>";
            emailService.SendEmail(email, "UKSF Notification", message);
        }
    }
}
