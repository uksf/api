using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSF.Api.Base.Services;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services.Data;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;

namespace UKSF.Api.Personnel.Services {
    public interface INotificationsService : IDataBackedService<INotificationsDataService> {
        void Add(Notification notification);
        Task SendTeamspeakNotification(Account account, string rawMessage);
        Task SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage);
        IEnumerable<Notification> GetNotificationsForContext();
        Task MarkNotificationsAsRead(List<string> ids);
        Task Delete(List<string> ids);
    }

    public class NotificationsService : DataBackedService<INotificationsDataService>, INotificationsService {
        private readonly IAccountService accountService;
        private readonly IEmailService emailService;
        private readonly IHubContext<NotificationHub, INotificationsClient> notificationsHub;
        private readonly IHttpContextService httpContextService;
        private readonly IObjectIdConversionService objectIdConversionService;

        private readonly ITeamspeakService teamspeakService;

        public NotificationsService(INotificationsDataService data, ITeamspeakService teamspeakService, IAccountService accountService, IEmailService emailService, IHubContext<NotificationHub, INotificationsClient> notificationsHub, IHttpContextService httpContextService, IObjectIdConversionService objectIdConversionService) : base(data) {
            this.teamspeakService = teamspeakService;
            this.accountService = accountService;

            this.emailService = emailService;
            this.notificationsHub = notificationsHub;
            this.httpContextService = httpContextService;
            this.objectIdConversionService = objectIdConversionService;
        }

        public async Task SendTeamspeakNotification(Account account, string rawMessage) {
            rawMessage = rawMessage.Replace("<a href='", "[url]").Replace("'>", "[/url]");
            await teamspeakService.SendTeamspeakMessageToClient(account, rawMessage);
        }

        public async Task SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage) {
            rawMessage = rawMessage.Replace("<a href='", "[url]").Replace("'>", "[/url]");
            await teamspeakService.SendTeamspeakMessageToClient(clientDbIds, rawMessage);
        }

        public IEnumerable<Notification> GetNotificationsForContext() {
            string contextId = httpContextService.GetUserId();
            return Data.Get(x => x.owner == contextId);
        }

        public void Add(Notification notification) {
            if (notification == null) return;
            Task unused = AddNotificationAsync(notification);
        }

        public async Task MarkNotificationsAsRead(List<string> ids) {
            string contextId = httpContextService.GetUserId();
            await Data.UpdateMany(x => x.owner == contextId && ids.Contains(x.id), Builders<Notification>.Update.Set(x => x.read, true));
            await notificationsHub.Clients.Group(contextId).ReceiveRead(ids);
        }

        public async Task Delete(List<string> ids) {
            ids = ids.ToList();
            string contextId = httpContextService.GetUserId();
            await Data.DeleteMany(x => x.owner == contextId && ids.Contains(x.id));
            await notificationsHub.Clients.Group(contextId).ReceiveClear(ids);
        }

        private async Task AddNotificationAsync(Notification notification) {
            notification.message = objectIdConversionService.ConvertObjectIds(notification.message);
            await Data.Add(notification);
            Account account = accountService.Data.GetSingle(notification.owner);
            if (account.settings.notificationsEmail) {
                SendEmailNotification(account.email, $"{notification.message}{(notification.link != null ? $"<br><a href='https://uk-sf.co.uk{notification.link}'>https://uk-sf.co.uk{notification.link}</a>" : "")}");
            }

            if (account.settings.notificationsTeamspeak) {
                await SendTeamspeakNotification(account, $"{notification.message}{(notification.link != null ? $"\n[url]https://uk-sf.co.uk{notification.link}[/url]" : "")}");
            }
        }

        private void SendEmailNotification(string email, string message) {
            message += "<br><br><sub>You can opt-out of these emails by unchecking 'Email notifications' in your <a href='https://uk-sf.co.uk/profile'>Profile</a></sub>";
            emailService.SendEmail(email, "UKSF Notification", message);
        }
    }
}
