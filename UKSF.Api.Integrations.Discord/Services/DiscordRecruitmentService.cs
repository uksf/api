using Discord;
using Discord.WebSocket;
using UKSF.Api.Discord.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Commands;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Queries;
using UKSF.Api.Shared.Services;
using MembershipState = UKSF.Api.Shared.Models.MembershipState;

namespace UKSF.Api.Discord.Services;

public interface IDiscordRecruitmentService { }

public class DiscordRecruitmentService : DiscordBaseService, IDiscordRecruitmentService
{
    private const string ButtonIdReject = "reject";
    private const string ButtonIdDismissRejection = "dismissrejection";
    private readonly IAccountContext _accountContext;
    private readonly IDisplayNameService _displayNameService;
    private readonly IBuildUrlQuery _buildUrlQuery;
    private readonly IUpdateApplicationCommand _updateApplicationCommand;
    private readonly IUksfLogger _logger;
    private readonly IVariablesService _variablesService;

    public DiscordRecruitmentService(
        IDiscordClientService discordClientService,
        IHttpContextService httpContextService,
        IVariablesService variablesService,
        IAccountContext accountContext,
        IDisplayNameService displayNameService,
        IBuildUrlQuery buildUrlQuery,
        IUpdateApplicationCommand updateApplicationCommand,
        IUksfLogger logger
    ) : base(discordClientService, httpContextService, variablesService, logger)
    {
        _variablesService = variablesService;
        _accountContext = accountContext;
        _displayNameService = displayNameService;
        _buildUrlQuery = buildUrlQuery;
        _updateApplicationCommand = updateApplicationCommand;
        _logger = logger;
    }

    public override void Activate()
    {
        var client = GetClient();
        client.UserLeft += ClientOnUserLeft;
        client.ButtonExecuted += ClientOnButtonExecuted;
    }

    private Task ClientOnUserLeft(SocketGuild _, SocketUser user)
    {
        return WrapEventTask(
            async () =>
            {
                var domainAccount = _accountContext.GetSingle(x => x.DiscordId == user.Id.ToString());
                if (domainAccount is { MembershipState: MembershipState.CONFIRMED, Application.State: ApplicationState.WAITING })
                {
                    var channelId = _variablesService.GetVariable("DID_C_SR1").AsUlong();
                    var guild = GetGuild();
                    var channel = guild.GetTextChannel(channelId);

                    var name = _displayNameService.GetDisplayName(domainAccount);
                    var applicationUrl = _buildUrlQuery.Web($"recruitment/{domainAccount.Id}");
                    var message = $"{name} left Discord\nWould you like me to reject their application?";
                    var builder = new ComponentBuilder().WithButton("View application", style: ButtonStyle.Link, url: applicationUrl)
                                                        .WithButton("Reject", BuildButtonData(ButtonIdReject, domainAccount.Id), ButtonStyle.Danger)
                                                        .WithButton("Dismiss", BuildButtonData(ButtonIdDismissRejection, domainAccount.Id));
                    await channel.SendMessageAsync(message, components: builder.Build());
                }
            }
        );
    }

    private Task ClientOnButtonExecuted(SocketMessageComponent component)
    {
        return WrapEventTask(
            async () =>
            {
                var buttonData = GetButtonData(component.Data.CustomId);
                await HandleButton(component, buttonData);
            }
        );
    }

    private Task HandleButton(IComponentInteraction component, DiscordButtonData buttonData)
    {
        return buttonData.Id switch
        {
            ButtonIdReject           => HandleButtonReject(component, buttonData),
            ButtonIdDismissRejection => HandleButtonDismissRejection(component, buttonData),
            _                        => Task.CompletedTask
        };
    }

    private async Task HandleButtonReject(IComponentInteraction component, DiscordButtonData buttonData)
    {
        var instigatorAccount = _accountContext.GetSingle(x => x.DiscordId == component.User.Id.ToString());
        _logger.LogAudit("Discord application rejection started", instigatorAccount.Id);

        var accountId = buttonData.Data.FirstOrDefault();
        if (string.IsNullOrEmpty(accountId))
        {
            _logger.LogError("Discord application rejection button was pressed but had invalid data");
            await component.RespondAsync("Hmm, that didn't work. Try again or contact an admin", ephemeral: true);
            return;
        }

        var applicationUrl = _buildUrlQuery.Web($"recruitment/{accountId}");
        var updatedComponent = new ComponentBuilder().WithButton("View application", style: ButtonStyle.Link, url: applicationUrl).Build();
        await component.Message.ModifyAsync(properties => properties.Components = updatedComponent);

        var domainAccount = _accountContext.GetSingle(accountId);
        if (domainAccount == null)
        {
            _logger.LogError($"Discord application rejection button tried to reject a null account ({accountId})");
            await component.RespondAsync("Hmm, that didn't work. Contact an admin", ephemeral: true);
            return;
        }

        var name = _displayNameService.GetDisplayName(domainAccount);
        if (domainAccount is { Application.State: ApplicationState.ACCEPTED } or { Application.State: ApplicationState.REJECTED })
        {
            _logger.LogError($"Discord application rejection button tried to reject an already accepted/rejected account ({accountId})");
            await component.RespondAsync($"Hmm, that didn't work. It looks like {name}'s application has already been accepted or rejected", ephemeral: true);
            return;
        }

        await _updateApplicationCommand.ExecuteAsync(accountId, ApplicationState.REJECTED);
        await component.RespondAsync($"Ok, I've rejected {name}'s application");
    }

    private async Task HandleButtonDismissRejection(IComponentInteraction component, DiscordButtonData buttonData)
    {
        var accountId = buttonData.Data.FirstOrDefault();
        if (string.IsNullOrEmpty(accountId))
        {
            _logger.LogError("Discord application rejection dismissal button was pressed but had invalid data");
            await component.RespondAsync("Hmm, that didn't work. Try again or contact an admin", ephemeral: true);
            return;
        }

        var instigatorAccount = _accountContext.GetSingle(x => x.DiscordId == component.User.Id.ToString());
        _logger.LogAudit("Discord application rejection dismissed", instigatorAccount.Id);

        var applicationUrl = _buildUrlQuery.Web($"recruitment/{accountId}");
        var builder = new ComponentBuilder().WithButton("View application", style: ButtonStyle.Link, url: applicationUrl);
        await component.Message.ModifyAsync(properties => properties.Components = builder.Build());
        await component.RespondAsync("Ok, I've dismissed the rejection");
    }
}
