using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.Services;

internal static class WorkshopModPaths
{
    private const string DependenciesFolderName = "@uksf_dependencies";

    public static string WorkshopMod(IVariablesService variablesService, string workshopModId)
    {
        var steamPath = variablesService.GetVariable("SERVER_PATH_STEAM").AsString();
        return Path.Combine(steamPath, "steamapps", "workshop", "content", "107410", workshopModId);
    }

    public static List<string> Repos(IVariablesService variablesService)
    {
        return
        [
            Path.Combine(variablesService.GetVariable("MODPACK_PATH_DEV").AsString(), "Repo"),
            Path.Combine(variablesService.GetVariable("MODPACK_PATH_RC").AsString(), "Repo")
        ];
    }

    public static string Dependencies(string repoPath)
    {
        return Path.Combine(repoPath, DependenciesFolderName);
    }

    public static string DependenciesAddons(string repoPath)
    {
        return Path.Combine(repoPath, DependenciesFolderName, "addons");
    }
}
