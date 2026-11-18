namespace UKSF.Api.Modpack.BuildProcess.Steps.Common;

public static class AddonFileCase
{
    public static StringComparer RepoPathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string ModRelativePath(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!parts[0].Equals("addons", StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        parts[0] = "addons";
        if (parts.Length == 2)
        {
            parts[1] = parts[1].ToLowerInvariant();
        }

        return Path.Join(parts);
    }

    public static List<string> LowercaseAddons(string repoPath)
    {
        List<string> renamed = [];
        foreach (var modFolder in new DirectoryInfo(repoPath).EnumerateDirectories("@*"))
        {
            var addonsFolder = modFolder.EnumerateDirectories().SingleOrDefault(x => x.Name.Equals("addons", StringComparison.OrdinalIgnoreCase));
            if (addonsFolder == null)
            {
                continue;
            }

            var addonsPath = Path.Join(modFolder.FullName, "addons");
            if (addonsFolder.Name != "addons")
            {
                MoveCaseOnly(addonsFolder.FullName, addonsPath, Directory.Move);
                renamed.Add(Path.Join(modFolder.Name, "addons"));
            }

            foreach (var file in new DirectoryInfo(addonsPath).EnumerateFiles().ToList())
            {
                var lowerName = file.Name.ToLowerInvariant();
                if (lowerName == file.Name)
                {
                    continue;
                }

                var target = Path.Join(addonsPath, lowerName);
                if (File.Exists(target) && !OperatingSystem.IsWindows())
                {
                    throw new IOException($"Cannot lowercase '{file.FullName}': '{target}' already exists");
                }

                MoveCaseOnly(file.FullName, target, (from, to) => File.Move(from, to));
                renamed.Add(Path.Join(modFolder.Name, "addons", lowerName));
            }
        }

        return renamed;
    }

    private static void MoveCaseOnly(string from, string to, Action<string, string> move)
    {
        var temporary = $"{to}.{Guid.NewGuid():N}.tmp";
        move(from, temporary);
        move(temporary, to);
    }
}
