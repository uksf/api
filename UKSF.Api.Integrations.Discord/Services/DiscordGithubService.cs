using Discord;
using Discord.WebSocket;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Request;
using UKSF.Api.Core.Services;
using MembershipState = UKSF.Api.Core.Models.MembershipState;

namespace UKSF.Api.Integrations.Discord.Services;

public interface IDiscordGithubService : IDiscordService
{
    Task DeleteCommands();
}

public class DiscordGithubService(
    IDiscordClientService discordClientService,
    IHttpContextService httpContextService,
    IVariablesService variablesService,
    IGithubIssuesService githubIssuesService,
    IAccountContext accountContext,
    IUksfLogger logger
) : DiscordBaseService(discordClientService, accountContext, httpContextService, variablesService, logger), IDiscordGithubService
{
    private readonly IUksfLogger _logger = logger;
    private const string NewGithubIssueCommandName = "new-github-issue";

    public void Activate()
    {
        var client = GetClient();
        client.SlashCommandExecuted += OnSlashCommand;
        client.SelectMenuExecuted += OnSelectMenu;
        client.ModalSubmitted += OnModalSubmitted;
    }

    public async Task CreateCommands()
    {
        var guild = GetGuild();
        var commands = await guild.GetApplicationCommandsAsync();

        if (commands.FirstOrDefault(x => x.Name == NewGithubIssueCommandName) is null)
        {
            var command = new SlashCommandBuilder().WithName(NewGithubIssueCommandName).WithDescription("Create a new GitHub issue for the Modpack");

            await guild.CreateApplicationCommandAsync(command.Build());
        }
    }

    public async Task DeleteCommands()
    {
        var guild = GetGuild();
        var commands = await guild.GetApplicationCommandsAsync();

        var githubIssueCommand = commands.FirstOrDefault(x => x.Name == NewGithubIssueCommandName);
        if (githubIssueCommand is not null)
        {
            await githubIssueCommand.DeleteAsync();
        }
    }

    private Task OnSlashCommand(SocketSlashCommand command)
    {
        return WrapEventTask(
            async () =>
            {
                if (command.Data.Name == NewGithubIssueCommandName)
                {
                    await OnNewGithubIssueCommand(command);
                }
            }
        );
    }

    private async Task OnNewGithubIssueCommand(SocketSlashCommand command)
    {
        var account = GetAccountForDiscordUser(command.User.Id);
        if (account is not { MembershipState: MembershipState.Member })
        {
            await command.RespondAsync("You do not have permission to use that command", ephemeral: true);
            return;
        }

        var issueTemplates = await githubIssuesService.GetIssueTemplates();
        var selectBuilder = new SelectMenuBuilder().WithCustomId("issue-template-select").WithPlaceholder("Select issue template");
        foreach (var issueTemplate in issueTemplates)
        {
            selectBuilder.AddOption(issueTemplate.Name, issueTemplate.Name, issueTemplate.Description);
        }

        var componentBuilder = new ComponentBuilder().WithSelectMenu(selectBuilder);
        await command.RespondAsync("Select an issue template", components: componentBuilder.Build(), ephemeral: true);
    }

    private Task OnSelectMenu(SocketMessageComponent message)
    {
        return WrapEventTask(
            async () =>
            {
                var issueTemplates = await githubIssuesService.GetIssueTemplates();
                var issueTemplateName = string.Join("", message.Data.Values);
                var issueTemplate = issueTemplates.FirstOrDefault(x => x.Name == issueTemplateName);
                if (issueTemplate is null)
                {
                    _logger.LogError($"Failed to find issue template with name {issueTemplateName}");
                    await message.UpdateAsync(
                        properties =>
                        {
                            properties.Components = null;
                            properties.Content = "Something went wrong, couldn't find the issue template";
                        }
                    );
                    return;
                }

                var modalBuilder = new ModalBuilder().WithTitle($"New {issueTemplate.Name} Issue")
                                                     .WithCustomId(issueTemplate.Name)
                                                     .AddTextInput("Issue Title", "issue-title", value: issueTemplate.Title, required: true)
                                                     .AddTextInput(
                                                         "Issue Description",
                                                         "issue-description",
                                                         TextInputStyle.Paragraph,
                                                         value: issueTemplate.Body,
                                                         required: true
                                                     );
                await message.RespondWithModalAsync(modalBuilder.Build());
                await message.ModifyOriginalResponseAsync(
                    properties =>
                    {
                        properties.Components = null;
                        properties.Content = $"Creating new {issueTemplate.Name} issue";
                    }
                );
            }
        );
    }

    private Task OnModalSubmitted(SocketModal modal)
    {
        return WrapEventTask(
            async () =>
            {
                SetUserContextByDiscordUser(modal.User.Id);

                var issueTemplates = await githubIssuesService.GetIssueTemplates();
                var issueTemplateName = modal.Data.CustomId;
                var issueTemplate = issueTemplates.FirstOrDefault(x => x.Name == issueTemplateName);
                if (issueTemplate is null)
                {
                    _logger.LogError($"Failed to find issue template with name {issueTemplateName}");
                    await modal.UpdateAsync(
                        properties =>
                        {
                            properties.Components = null;
                            properties.Content = "Something went wrong, couldn't find the issue template";
                        }
                    );
                    return;
                }

                var issueTitle = modal.Data.Components.First(x => x.CustomId == "issue-title").Value;
                var issueBody = modal.Data.Components.First(x => x.CustomId == "issue-description").Value;
                try
                {
                    var issue = await githubIssuesService.CreateIssue(new NewIssueRequest(issueTitle, issueTemplate.Labels, issueBody));

                    _logger.LogAudit($"Created new github issue {issue.HtmlUrl}");
                    await modal.UpdateAsync(
                        properties =>
                        {
                            properties.Components = null;
                            properties.Content = $"New Github issue created: [{issue.Title}]({issue.HtmlUrl})";
                        }
                    );
                }
                catch (Exception exception)
                {
                    _logger.LogError("Failed to create issue", exception);
                    await modal.UpdateAsync(
                        properties =>
                        {
                            properties.Components = null;
                            properties.Content = "Failed to create issue";
                        }
                    );
                }
            }
        );
    }
}
