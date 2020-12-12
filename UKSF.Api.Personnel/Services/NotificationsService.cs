using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Services {
    public interface INotificationsService {
        void Add(Notification notification);
        void SendTeamspeakNotification(Account account, string rawMessage);
        void SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage);
        IEnumerable<Notification> GetNotificationsForContext();
        Task MarkNotificationsAsRead(List<string> ids);
        Task Delete(List<string> ids);
    }

    public class NotificationsService : INotificationsService {
        private readonly IAccountContext _accountContext;
        private readonly IEmailService _emailService;
        private readonly IHttpContextService _httpContextService;
        private readonly INotificationsContext _notificationsContext;
        private readonly IHubContext<NotificationHub, INotificationsClient> _notificationsHub;
        private readonly IObjectIdConversionService _objectIdConversionService;
        private readonly IEventBus _eventBus;
        private readonly IVariablesService _variablesService;

        public NotificationsService(
            IAccountContext accountContext,
            INotificationsContext notificationsContext,
            IEmailService emailService,
            IHubContext<NotificationHub, INotificationsClient> notificationsHub,
            IHttpContextService httpContextService,
            IObjectIdConversionService objectIdConversionService,
            IEventBus eventBus,
            IVariablesService variablesService
        ) {
            _accountContext = accountContext;
            _notificationsContext = notificationsContext;
            _emailService = emailService;
            _notificationsHub = notificationsHub;
            _httpContextService = httpContextService;
            _objectIdConversionService = objectIdConversionService;
            _eventBus = eventBus;
            _variablesService = variablesService;
        }

        public void SendTeamspeakNotification(Account account, string rawMessage) {
            if (NotificationsDisabled()) return;

            if (account.TeamspeakIdentities == null) return;
            if (account.TeamspeakIdentities.Count == 0) return;

            SendTeamspeakNotification(account.TeamspeakIdentities, rawMessage);
        }

        public void SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage) {
            if (NotificationsDisabled()) return;

            rawMessage = rawMessage.Replace("<a href='", "[url]").Replace("'>", "[/url]");
            _eventBus.Send(new TeamspeakMessageEventData(clientDbIds, rawMessage));
        }

        public IEnumerable<Notification> GetNotificationsForContext() {
            string contextId = _httpContextService.GetUserId();
            return _notificationsContext.Get(x => x.Owner == contextId);
        }

        public void Add(Notification notification) {
            if (notification == null) return;
            Task unused = AddNotificationAsync(notification);
        }

        public async Task MarkNotificationsAsRead(List<string> ids) {
            string contextId = _httpContextService.GetUserId();
            await _notificationsContext.UpdateMany(x => x.Owner == contextId && ids.Contains(x.Id), Builders<Notification>.Update.Set(x => x.Read, true));
            await _notificationsHub.Clients.Group(contextId).ReceiveRead(ids);
        }

        public async Task Delete(List<string> ids) {
            ids = ids.ToList();
            string contextId = _httpContextService.GetUserId();
            await _notificationsContext.DeleteMany(x => x.Owner == contextId && ids.Contains(x.Id));
            await _notificationsHub.Clients.Group(contextId).ReceiveClear(ids);
        }

        private async Task AddNotificationAsync(Notification notification) {
            notification.Message = _objectIdConversionService.ConvertObjectIds(notification.Message);
            Account account = _accountContext.GetSingle(notification.Owner);
            if (account.MembershipState == MembershipState.DISCHARGED) {
                return;
            }

            await _notificationsContext.Add(notification);
            if (account.Settings.NotificationsEmail) {
                SendEmailNotification(
                    account.Email,
                    $"{notification.Message}{(notification.Link != null ? $"<br><a href='https://uk-sf.co.uk{notification.Link}'>https://uk-sf.co.uk{notification.Link}</a>" : "")}"
                );
            }

            if (account.Settings.NotificationsTeamspeak) {
                SendTeamspeakNotification(account, $"{notification.Message}{(notification.Link != null ? $"\n[url]https://uk-sf.co.uk{notification.Link}[/url]" : "")}");
            }
        }

        private void SendEmailNotification(string email, string message) {
            if (NotificationsDisabled()) return;

            message += "<br><br><sub>You can opt-out of these emails by unchecking 'Email notifications' in your <a href='https://uk-sf.co.uk/profile'>Profile</a></sub>";
            _emailService.SendEmail(email, "UKSF Notification", message);
        }

        private bool NotificationsDisabled() => !_variablesService.GetFeatureState("NOTIFICATIONS");
    }
}
