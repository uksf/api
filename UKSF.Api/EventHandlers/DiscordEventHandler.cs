using MongoDB.Bson;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.EventHandlers;

public interface IDiscordEventHandler : IEventHandler;

public class DiscordEventHandler : IDiscordEventHandler
{
    private readonly IAccountContext _accountContext;
    private readonly ICommentThreadService _commentThreadService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IEventBus _eventBus;
    private readonly IUksfLogger _logger;

    public DiscordEventHandler(
        IEventBus eventBus,
        ICommentThreadService commentThreadService,
        IAccountContext accountContext,
        IDisplayNameService displayNameService,
        IUksfLogger logger
    )
    {
        _eventBus = eventBus;
        _commentThreadService = commentThreadService;
        _accountContext = accountContext;
        _displayNameService = displayNameService;
        _logger = logger;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<DiscordEventData>(HandleEvent, _logger.LogError);
    }

    private async Task HandleEvent(EventModel eventModel, DiscordEventData discordEventData)
    {
        switch (discordEventData.EventType)
        {
            case DiscordUserEventType.Joined:          break;
            case DiscordUserEventType.Left:            await LeftEvent(discordEventData.EventData); break;
            case DiscordUserEventType.Banned:          break;
            case DiscordUserEventType.Unbanned:        break;
            case DiscordUserEventType.Message_Deleted: break;
        }
    }

    private async Task LeftEvent(string accountId)
    {
        var account = _accountContext.GetSingle(accountId);
        if (account.MembershipState is MembershipState.Discharged or MembershipState.Unconfirmed || account.Application?.State is not ApplicationState.Rejected)
        {
            return;
        }

        var name = _displayNameService.GetDisplayName(account);
        await _commentThreadService.InsertComment(
            account.Application.RecruiterCommentThread,
            new DomainComment
            {
                Author = ObjectId.Empty.ToString(),
                Content = $"{name} left Discord",
                Timestamp = DateTime.UtcNow
            }
        );
    }
}
