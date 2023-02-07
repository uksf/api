using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Queries;
using UKSF.Api.Core.Signalr.Clients;
using UKSF.Api.Core.Signalr.Hubs;

namespace UKSF.Api.Core.Services;

public interface INotificationsService
{
    void Add(Notification notification);
    void SendTeamspeakNotification(IEnumerable<int> clientDbIds, string rawMessage);
    IEnumerable<Notification> GetNotificationsForContext();
    Task MarkNotificationsAsRead(List<string> ids);
    Task Delete(List<string> ids);
}

public class NotificationsService : INotificationsService
{
    private readonly IAccountContext _accountContext;
    private readonly IEventBus _eventBus;
    private readonly IHttpContextService _httpContextService;
    private readonly INotificationsContext _notificationsContext;
    private readonly IHubContext<NotificationHub, INotificationsClient> _notificationsHub;
    private readonly IObjectIdConversionService _objectIdConversionService;
    private readonly ISendTemplatedEmailCommand _sendTemplatedEmailCommand;
    private readonly IVariablesService _variablesService;

    public NotificationsService(
        IAccountContext accountContext,
        INotificationsContext notificationsContext,
        ISendTemplatedEmailCommand sendTemplatedEmailCommand,
        IHubContext<NotificationHub, INotificationsClient> notificationsHub,
        IHttpContextService httpContextService,
        IObjectIdConversionService objectIdConversionService,
        IEventBus eventBus,
        IVariablesService variablesService
    )
    {
        _accountContext = accountContext;
        _notificationsContext = notificationsContext;
        _sendTemplatedEmailCommand = sendTemplatedEmailCommand;
        _notificationsHub = notificationsHub;
        _httpContextService = httpContextService;
        _objectIdConversionService = objectIdConversionService;
        _eventBus = eventBus;
        _variablesService = variablesService;
    }

    public void SendTeamspeakNotification(IEnumerable<int> clientDbIds, string rawMessage)
    {
        if (NotificationsGloballyDisabled())
        {
            return;
        }

        rawMessage = rawMessage.Replace("<a href='", "[url]").Replace("'>", "[/url]");
        _eventBus.Send(new TeamspeakMessageEventData(clientDbIds, rawMessage));
    }

    public IEnumerable<Notification> GetNotificationsForContext()
    {
        var contextId = _httpContextService.GetUserId();
        return _notificationsContext.Get(x => x.Owner == contextId);
    }

    public void Add(Notification notification)
    {
        if (notification == null)
        {
            return;
        }

        var unused = AddNotificationAsync(notification);
    }

    public async Task MarkNotificationsAsRead(List<string> ids)
    {
        var contextId = _httpContextService.GetUserId();
        await _notificationsContext.UpdateMany(x => x.Owner == contextId && ids.Contains(x.Id), Builders<Notification>.Update.Set(x => x.Read, true));
        await _notificationsHub.Clients.Group(contextId).ReceiveRead(ids);
    }

    public async Task Delete(List<string> ids)
    {
        ids = ids.ToList();
        var contextId = _httpContextService.GetUserId();
        await _notificationsContext.DeleteMany(x => x.Owner == contextId && ids.Contains(x.Id));
        await _notificationsHub.Clients.Group(contextId).ReceiveClear(ids);
    }

    private async Task AddNotificationAsync(Notification notification)
    {
        notification.Message = _objectIdConversionService.ConvertObjectIds(notification.Message);
        var domainAccount = _accountContext.GetSingle(notification.Owner);
        if (domainAccount.MembershipState == MembershipState.DISCHARGED)
        {
            return;
        }

        await _notificationsContext.Add(notification);
        await SendEmailNotification(
            domainAccount,
            $"{notification.Message}{(notification.Link != null ? $"<br><a href='https://uk-sf.co.uk{notification.Link}'>https://uk-sf.co.uk{notification.Link}</a>" : "")}"
        );

        SendTeamspeakNotification(
            domainAccount,
            $"{notification.Message}{(notification.Link != null ? $"\n[url]https://uk-sf.co.uk{notification.Link}[/url]" : "")}"
        );
    }

    private async Task SendEmailNotification(DomainAccount domainAccount, string message)
    {
        if (NotificationsGloballyDisabled() || !domainAccount.Settings.NotificationsEmail)
        {
            return;
        }

        await _sendTemplatedEmailCommand.ExecuteAsync(
            new(domainAccount.Email, "UKSF Notification", TemplatedEmailNames.NotificationTemplate, new() { { "message", message } })
        );
    }

    private void SendTeamspeakNotification(DomainAccount domainAccount, string rawMessage)
    {
        if (NotificationsGloballyDisabled() || !domainAccount.Settings.NotificationsTeamspeak || domainAccount.TeamspeakIdentities.IsNullOrEmpty())
        {
            return;
        }

        SendTeamspeakNotification(domainAccount.TeamspeakIdentities, rawMessage);
    }

    private bool NotificationsGloballyDisabled()
    {
        return !_variablesService.GetFeatureState("NOTIFICATIONS");
    }
}
