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

public class NotificationsService(
    IAccountContext accountContext,
    INotificationsContext notificationsContext,
    ISendTemplatedEmailCommand sendTemplatedEmailCommand,
    IHubContext<NotificationHub, INotificationsClient> notificationsHub,
    IHttpContextService httpContextService,
    IObjectIdConversionService objectIdConversionService,
    IEventBus eventBus,
    IVariablesService variablesService
) : INotificationsService
{
    public void SendTeamspeakNotification(IEnumerable<int> clientDbIds, string rawMessage)
    {
        if (NotificationsGloballyDisabled())
        {
            return;
        }

        rawMessage = rawMessage.Replace("<a href='", "[url]").Replace("'>", "[/url]");
        eventBus.Send(new TeamspeakMessageEventData(clientDbIds, rawMessage), nameof(SendTeamspeakNotification));
    }

    public IEnumerable<Notification> GetNotificationsForContext()
    {
        var contextId = httpContextService.GetUserId();
        return notificationsContext.Get(x => x.Owner == contextId);
    }

    public void Add(Notification notification)
    {
        if (notification == null)
        {
            return;
        }

        _ = AddNotificationAsync(notification);
    }

    public async Task MarkNotificationsAsRead(List<string> ids)
    {
        var contextId = httpContextService.GetUserId();
        await notificationsContext.UpdateMany(x => x.Owner == contextId && ids.Contains(x.Id), Builders<Notification>.Update.Set(x => x.Read, true));
        await notificationsHub.Clients.Group(contextId).ReceiveRead(ids);
    }

    public async Task Delete(List<string> ids)
    {
        ids = ids.ToList();
        var contextId = httpContextService.GetUserId();
        await notificationsContext.DeleteMany(x => x.Owner == contextId && ids.Contains(x.Id));
        await notificationsHub.Clients.Group(contextId).ReceiveClear(ids);
    }

    private async Task AddNotificationAsync(Notification notification)
    {
        notification.Message = objectIdConversionService.ConvertObjectIds(notification.Message);
        var domainAccount = accountContext.GetSingle(notification.Owner);
        if (domainAccount.MembershipState == MembershipState.DISCHARGED)
        {
            return;
        }

        await notificationsContext.Add(notification);
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

        await sendTemplatedEmailCommand.ExecuteAsync(
            new SendTemplatedEmailCommandArgs(
                domainAccount.Email,
                "UKSF Notification",
                TemplatedEmailNames.NotificationTemplate,
                new Dictionary<string, string> { { "message", message } }
            )
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
        return !variablesService.GetFeatureState("NOTIFICATIONS");
    }
}
