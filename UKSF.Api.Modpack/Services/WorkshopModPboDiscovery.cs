using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.Services;

/// <summary>
///     Finds a workshop mod's PBOs. Only the top-level addons folder is a convention every mod follows; PBOs anywhere else
///     (optionals, compat folders) are the author's own layout, so they are offered but reported with their folder.
/// </summary>
internal static class WorkshopModPboDiscovery
{
    private const string AddonsFolderName = "addons";

    public sealed record PboFile(string Name, string Folder, string FullPath, bool InAddons);

    /// <summary>
    ///     The dependencies folder is flat, so a name can only be one file. A PBO outside addons that shares a name with one
    ///     inside is left out, and so is a name found in more than one folder outside addons.
    /// </summary>
    public static List<PboFile> Find(IFileSystemService fileSystemService, string workshopModPath)
    {
        if (!fileSystemService.DirectoryExists(workshopModPath))
        {
            return [];
        }

        var files = fileSystemService.EnumerateFiles(workshopModPath, "*.pbo", SearchOption.AllDirectories)
                                     .Select(path => ToPboFile(workshopModPath, path))
                                     .ToList();

        var addonsFiles = files.Where(x => x.InAddons).ToList();
        var duplicates = addonsFiles.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count != 0)
        {
            throw new InvalidOperationException($"Duplicate PBO names found: {string.Join(", ", duplicates)}. Manual investigation required.");
        }

        var addonsNames = addonsFiles.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var otherFiles = files.Where(x => !x.InAddons && !addonsNames.Contains(x.Name))
                              .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                              .Where(g => g.Count() == 1)
                              .Select(g => g.Single());

        return [.. addonsFiles, .. otherFiles];
    }

    private static PboFile ToPboFile(string workshopModPath, string path)
    {
        var folder = Path.GetRelativePath(workshopModPath, Path.GetDirectoryName(path)!).Replace('\\', '/');
        if (folder == ".")
        {
            folder = "";
        }

        var inAddons = folder.Split('/')[0].Equals(AddonsFolderName, StringComparison.OrdinalIgnoreCase);
        return new PboFile(Path.GetFileName(path), folder, path, inAddons);
    }
}
