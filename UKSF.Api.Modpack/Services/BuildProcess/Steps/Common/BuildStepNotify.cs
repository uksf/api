using UKSF.Api.Admin.Extensions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Discord.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.Common;

[BuildStep(Name)]
public class BuildStepNotify : BuildStep
{
    public const string Name = "Notify";
    private IDiscordService _discordService;
    private IReleaseService _releaseService;

    protected override Task SetupExecute()
    {
        _discordService = ServiceProvider.GetService<IDiscordService>();
        _releaseService = ServiceProvider.GetService<IReleaseService>();
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
                await _discordService.SendMessageToEveryone(VariablesService.GetVariable("DID_C_MODPACK_RELEASE").AsUlong(), GetDiscordMessage(release));
                break;
            }
            case GameEnvironment.RC:
                await _discordService.SendMessage(VariablesService.GetVariable("DID_C_MODPACK_DEV").AsUlong(), GetDiscordMessage());
                break;
            case GameEnvironment.DEV: break;
            default:                  throw new ArgumentOutOfRangeException();
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
