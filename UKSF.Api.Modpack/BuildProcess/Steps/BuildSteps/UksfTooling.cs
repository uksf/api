using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

public static class UksfTooling
{
    public const string PathVariable = "BUILD_PATH_TOOLING";

    public static string FileName(string tool)
    {
        return OperatingSystem.IsWindows() ? $"{tool}.exe" : tool;
    }

    public static string Find(IVariablesService variablesService, string tool)
    {
        var path = Path.Join(variablesService.GetVariable(PathVariable).AsString(), FileName(tool));
        if (!File.Exists(path))
        {
            throw new Exception($"{tool} not found at '{path}' (variable {PathVariable})");
        }

        return path;
    }
}
