using System.Text;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Discord.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepNotify : BuildStep
{
    public const string Name = "Notify";
    private IClock _clock;
    private IDiscordMessageService _discordMessageService;
    private IDiscordTextService _discordTextService;
    private IReleaseService _releaseService;
    private bool _tagEveryone;

    protected override Task SetupExecute()
    {
        _discordTextService = ServiceProvider.GetService<IDiscordTextService>();
        _discordMessageService = ServiceProvider.GetService<IDiscordMessageService>();
        _releaseService = ServiceProvider.GetService<IReleaseService>();
        _clock = ServiceProvider.GetService<IClock>();

        _tagEveryone = VariablesService.GetVariable("MODPACK_NOTIFY_TAG_EVERYONE").AsBoolWithDefault(false);

        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    protected override async Task ProcessExecute()
    {
        var release = _releaseService.GetRelease(Build.Version);
        var message = GetDiscordMessage(release);
        var releaseChannel = VariablesService.GetVariable("DID_C_MODPACK_RELEASE").AsUlong();

        var isAllowedAtThisTime = _clock.UkNow().Hour >= 10 && _clock.UkNow().Hour < 22;
        var notifyOffline = _tagEveryone && isAllowedAtThisTime;
        await _discordMessageService.SendMessageToEveryone(releaseChannel, message, notifyOffline);

        StepLogger.Log("Notifications sent");
    }

    private string GetDiscordMessage(ModpackRelease release)
    {
        var changelogText = _discordTextService.FromMarkdown(release.Changelog);
        var changelog = _discordTextService.ToQuote(changelogText);
        changelog = changelog.Replace("SR3 - Development Team", "*SR3 - Development Team*");

        return new StringBuilder().Append($"Modpack Update - {release.Version}")
                                  .Append($"\nChangelog: <https://uk-sf.co.uk/modpack/releases?version={release.Version}>")
                                  .Append($"\n\n{changelog}")
                                  .ToString();
    }
}
