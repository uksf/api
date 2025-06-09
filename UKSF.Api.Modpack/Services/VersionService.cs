using System.Text;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface IVersionService
{
    bool IsVersionValid(string versionString);
    bool IsVersionIncremental(string versionString, string previousVersionString);
    bool IsVersionNewer(string versionString, string referenceVersionString);
    string GetVersionFromVersionFileContent(string versionFileContent);
    string GetVersionFileContentFromVersion(string versionString);
}

public class VersionService : IVersionService
{
    public bool IsVersionValid(string versionString)
    {
        var version = new ModpackVersion(versionString);
        return version.IsValid;
    }

    public bool IsVersionIncremental(string versionString, string previousVersionString)
    {
        var version = new ModpackVersion(versionString);
        var previousVersion = new ModpackVersion(previousVersionString);

        return version.IsValidIncrementFrom(previousVersion);
    }

    public bool IsVersionNewer(string versionString, string referenceVersionString)
    {
        var version = new ModpackVersion(versionString);
        var referenceVersion = new ModpackVersion(referenceVersionString);

        return version.IsNewerThan(referenceVersion);
    }

    public string GetVersionFromVersionFileContent(string versionFileContent)
    {
        var versionParts = versionFileContent.Split("\n").Take(3).Select(x => x.Split(' ')[^1]);
        var version = string.Join('.', versionParts);
        return version;
    }

    public string GetVersionFileContentFromVersion(string versionString)
    {
        var version = new ModpackVersion(versionString);

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"#define MAJOR {version.Major}");
        stringBuilder.AppendLine($"#define MINOR {version.Minor}");
        stringBuilder.AppendLine($"#define PATCHLVL {version.Patch}");

        return stringBuilder.ToString();
    }
}
