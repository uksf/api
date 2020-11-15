using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Services {
    public interface INotificationsService : IDataBackedService<INotificationsDataService> {
        void Add(Notification notification);
        void SendTeamspeakNotification(Account account, string rawMessage);
        void SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage);
        IEnumerable<Notification> GetNotificationsForContext();
        Task MarkNotificationsAsRead(List<string> ids);
        Task Delete(List<string> ids);
    }

    public class NotificationsService : DataBackedService<INotificationsDataService>, INotificationsService {
        private readonly IAccountService _accountService;
        private readonly IEmailService _emailService;
        private readonly IHttpContextService _httpContextService;
        private readonly IHubContext<NotificationHub, INotificationsClient> _notificationsHub;
        private readonly IObjectIdConversionService _objectIdConversionService;
        private readonly IEventBus<TeamspeakMessageEventModel> _teamspeakMessageEventBus;

        public NotificationsService(
            INotificationsDataService data,
            IAccountService accountService,
            IEmailService emailService,
            IHubContext<NotificationHub, INotificationsClient> notificationsHub,
            IHttpContextService httpContextService,
            IObjectIdConversionService objectIdConversionService,
            IEventBus<TeamspeakMessageEventModel> teamspeakMessageEventBus
        ) : base(data) {
            _accountService = accountService;
            _emailService = emailService;
            _notificationsHub = notificationsHub;
            _httpContextService = httpContextService;
            _objectIdConversionService = objectIdConversionService;
            _teamspeakMessageEventBus = teamspeakMessageEventBus;
        }

        public void SendTeamspeakNotification(Account account, string rawMessage) {
            if (account.teamspeakIdentities == null) return;
            if (account.teamspeakIdentities.Count == 0) return;

            SendTeamspeakNotification(account.teamspeakIdentities, rawMessage);
        }

        public void SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage) {
            rawMessage = rawMessage.Replace("<a href='", "[url]").Replace("'>", "[/url]");
            _teamspeakMessageEventBus.Send(new TeamspeakMessageEventModel(clientDbIds, rawMessage));
        }

        public IEnumerable<Notification> GetNotificationsForContext() {
            string contextId = _httpContextService.GetUserId();
            return Data.Get(x => x.owner == contextId);
        }

        public void Add(Notification notification) {
            if (notification == null) return;
            Task unused = AddNotificationAsync(notification);
        }

        public async Task MarkNotificationsAsRead(List<string> ids) {
            string contextId = _httpContextService.GetUserId();
            await Data.UpdateMany(x => x.owner == contextId && ids.Contains(x.id), Builders<Notification>.Update.Set(x => x.read, true));
            await _notificationsHub.Clients.Group(contextId).ReceiveRead(ids);
        }

        public async Task Delete(List<string> ids) {
            ids = ids.ToList();
            string contextId = _httpContextService.GetUserId();
            await Data.DeleteMany(x => x.owner == contextId && ids.Contains(x.id));
            await _notificationsHub.Clients.Group(contextId).ReceiveClear(ids);
        }

        private async Task AddNotificationAsync(Notification notification) {
            notification.message = _objectIdConversionService.ConvertObjectIds(notification.message);
            await Data.Add(notification);
            Account account = _accountService.Data.GetSingle(notification.owner);
            if (account.settings.notificationsEmail) {
                SendEmailNotification(
                    account.email,
                    $"{notification.message}{(notification.link != null ? $"<br><a href='https://uk-sf.co.uk{notification.link}'>https://uk-sf.co.uk{notification.link}</a>" : "")}"
                );
            }

            if (account.settings.notificationsTeamspeak) {
                SendTeamspeakNotification(account, $"{notification.message}{(notification.link != null ? $"\n[url]https://uk-sf.co.uk{notification.link}[/url]" : "")}");
            }
        }

        private void SendEmailNotification(string email, string message) {
            message += "<br><br><sub>You can opt-out of these emails by unchecking 'Email notifications' in your <a href='https://uk-sf.co.uk/profile'>Profile</a></sub>";
            _emailService.SendEmail(email, "UKSF Notification", message);
        }
    }
}
