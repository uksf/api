using System.IO;

namespace UKSF.Api.ArmaServer.Services;

public static class DevRunArtifacts
{
    public const int RetainedProfileRuns = 20;
    private const string Prefix = "DevRun_";
    private const int RunKeyLength = 15;

    public static IReadOnlyList<string> CleanUp(string profilesRoot, string configsRoot, string missionsRoot)
    {
        var staleProfiles = Matching(profilesRoot, root => root.GetDirectories(Prefix + "*"))
                            .GroupBy(x => x.Name[..Math.Min(RunKeyLength, x.Name.Length)])
                            .OrderByDescending(run => run.Max(x => x.CreationTimeUtc))
                            .Skip(RetainedProfileRuns)
                            .SelectMany(run => run);

        return DeleteAll(
            Matching(missionsRoot, root => root.GetDirectories(Prefix + "*"))
                .Concat(Matching(configsRoot, root => root.GetFiles(Prefix + "*.cfg")))
                .Concat(staleProfiles)
        );
    }

    private static IEnumerable<FileSystemInfo> Matching(string root, Func<DirectoryInfo, FileSystemInfo[]> select)
    {
        return string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ? [] : select(new DirectoryInfo(root));
    }

    private static List<string> DeleteAll(IEnumerable<FileSystemInfo> entries)
    {
        var failures = new List<string>();
        foreach (var entry in entries.ToList())
        {
            try
            {
                if (entry is DirectoryInfo directory)
                {
                    directory.Delete(recursive: true);
                }
                else
                {
                    entry.Delete();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add($"{entry.FullName}: {ex.Message}");
            }
        }

        return failures;
    }
}
