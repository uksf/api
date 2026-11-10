using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace UKSF.Api.ArmaServer.Services;

public static partial class ArmaServerExecutable
{
    public static string FileName => OperatingSystem.IsWindows() ? "arma3server_x64.exe" : "arma3server_x64";

    public static string PathIn(string serverRoot)
    {
        return Path.Join(serverRoot, FileName);
    }

    public static string ReadGameVersion(string executablePath)
    {
        if (OperatingSystem.IsWindows())
        {
            var productVersion = FileVersionInfo.GetVersionInfo(executablePath).ProductVersion;
            return productVersion is null ? null : string.Join('.', productVersion.Split('.').Take(2));
        }

        var match = ConsoleVersionBanner().Match(Encoding.Latin1.GetString(File.ReadAllBytes(executablePath)));
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"Arma 3 Console version (\d+\.\d+)\.\d+")]
    private static partial Regex ConsoleVersionBanner();
}
