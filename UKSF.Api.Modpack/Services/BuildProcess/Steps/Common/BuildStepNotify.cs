using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Discord.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.Common;

[BuildStep(Name)]
public class BuildStepNotify : BuildStep
{
    public const string Name = "Notify";
    private IDiscordMessageService _discordMessageService;
    private IReleaseService _releaseService;
    private bool _tagEveryone;
    private IClock _clock;

    protected override Task SetupExecute()
    {
        _discordMessageService = ServiceProvider.GetService<IDiscordMessageService>();
        _releaseService = ServiceProvider.GetService<IReleaseService>();
        _clock = ServiceProvider.GetService<IClock>();

        _tagEveryone = VariablesService.GetVariable("MODPACK_NOTIFY_TAG_EVERYONE").AsBoolWithDefault(false);
        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    protected override async Task ProcessExecute()
    {
        switch (Build.Environment)
        {
            case GameEnvironment.RELEASE:
            {
                var release = _releaseService.GetRelease(Build.Version);
                var isAllowedAtThisTime = _clock.UtcNow().Hour > 10 || _clock.UtcNow().Hour > 22;
                if (_tagEveryone && isAllowedAtThisTime)
                {
                    await _discordMessageService.SendMessageToEveryone(
                        VariablesService.GetVariable("DID_C_MODPACK_RELEASE").AsUlong(),
                        GetDiscordMessage(release)
                    );
                }
                else
                {
                    await _discordMessageService.SendMessage(VariablesService.GetVariable("DID_C_MODPACK_RELEASE").AsUlong(), GetDiscordMessage(release));
                }
                break;
            }
            case GameEnvironment.RC:
                await _discordMessageService.SendMessage(VariablesService.GetVariable("DID_C_MODPACK_DEV").AsUlong(), GetDiscordMessage());
                break;
            case GameEnvironment.DEVELOPMENT: break;
            default:                          throw new ArgumentOutOfRangeException();
        }

        StepLogger.Log("Notifications sent");
    }

    private string GetBuildMessage()
    {
        return $"New release candidate available for {Build.Version} on the rc repository";
    }

    private string GetBuildLink()
    {
        return $"https://uk-sf.co.uk/modpack/builds-rc?version={Build.Version}&build={Build.Id}";
    }

    private string GetDiscordMessage(ModpackRelease release = null)
    {
        return release == null
            ? $"Modpack RC Build - {Build.Version} RC# {Build.BuildNumber}\n{GetBuildMessage()}\n<{GetBuildLink()}>"
            : $"Modpack Update - {release.Version}\nFull Changelog: <https://uk-sf.co.uk/modpack/releases?version={release.Version}>\n\nSummary:\n```{release.Description}```";
    }
}
