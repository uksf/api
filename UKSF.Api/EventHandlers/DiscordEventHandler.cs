using MongoDB.Bson;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.EventHandlers;

public interface IDiscordEventhandler : IEventHandler { }

public class DiscordEventhandler : IDiscordEventhandler
{
    private readonly IAccountContext _accountContext;
    private readonly ICommentThreadService _commentThreadService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IEventBus _eventBus;
    private readonly IUksfLogger _logger;

    public DiscordEventhandler(
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
            case DiscordUserEventType.JOINED: break;
            case DiscordUserEventType.LEFT:
                await LeftEvent(discordEventData.EventData);
                break;
            case DiscordUserEventType.BANNED:          break;
            case DiscordUserEventType.UNBANNED:        break;
            case DiscordUserEventType.MESSAGE_DELETED: break;
        }
    }

    private async Task LeftEvent(string accountId)
    {
        var domainAccount = _accountContext.GetSingle(accountId);
        if (domainAccount.MembershipState is MembershipState.DISCHARGED or MembershipState.UNCONFIRMED ||
            domainAccount.Application?.State is not ApplicationState.REJECTED)
        {
            return;
        }

        var name = _displayNameService.GetDisplayName(domainAccount);
        await _commentThreadService.InsertComment(
            domainAccount.Application.RecruiterCommentThread,
            new() { Author = ObjectId.Empty.ToString(), Content = $"{name} left Discord", Timestamp = DateTime.UtcNow }
        );
    }
}
