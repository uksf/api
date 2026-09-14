using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Backups.Services;

public static class BackupPaths
{
    public static string Normalise(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new UksfException("Backup path is required", 400);
        }

        var normalised = path.Trim().Replace('/', '\\');
        while (normalised.Contains(@"\\"))
        {
            normalised = normalised.Replace(@"\\", @"\");
        }

        if (normalised.Length > 3 && normalised.EndsWith('\\'))
        {
            normalised = normalised.TrimEnd('\\');
        }

        if (normalised.Length < 3 || normalised[1] != ':' || normalised[2] != '\\' || !char.IsLetter(normalised[0]))
        {
            throw new UksfException($"Backup path must be a local drive path: {path}", 400);
        }

        return char.ToUpperInvariant(normalised[0]) + normalised[1..];
    }

    /// <summary>Whether <paramref name="child" /> sits inside <paramref name="parent" />, or is the same path.</summary>
    public static bool Contains(string parent, string child)
    {
        var normalisedParent = Normalise(parent);
        var normalisedChild = Normalise(child);

        if (string.Equals(normalisedParent, normalisedChild, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = normalisedParent.EndsWith('\\') ? normalisedParent : normalisedParent + '\\';
        return normalisedChild.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
