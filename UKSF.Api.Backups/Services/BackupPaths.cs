using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Backups.Services;

public static class BackupPaths
{
    public static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Normalise(string path)
    {
        return Normalise(path, OperatingSystem.IsWindows());
    }

    public static bool Contains(string parent, string child)
    {
        return Contains(parent, child, OperatingSystem.IsWindows());
    }

    public static string ToEntryName(string path)
    {
        return ToEntryName(path, OperatingSystem.IsWindows());
    }

    public static string LastSegment(string normalisedPath)
    {
        return LastSegment(normalisedPath, OperatingSystem.IsWindows());
    }

    internal static string Normalise(string path, bool windows)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new UksfException("Backup path is required", 400);
        }

        var separator = SeparatorFor(windows);
        var doubled = new string(separator, 2);
        var normalised = path.Trim().Replace(windows ? '/' : '\\', separator);
        while (normalised.Contains(doubled))
        {
            normalised = normalised.Replace(doubled, separator.ToString());
        }

        return windows ? NormaliseWindows(path, normalised) : NormaliseUnix(path, normalised);
    }

    internal static bool Contains(string parent, string child, bool windows)
    {
        var normalisedParent = Normalise(parent, windows);
        var normalisedChild = Normalise(child, windows);
        var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (string.Equals(normalisedParent, normalisedChild, comparison))
        {
            return true;
        }

        var separator = SeparatorFor(windows);
        var prefix = normalisedParent.EndsWith(separator) ? normalisedParent : normalisedParent + separator;
        return normalisedChild.StartsWith(prefix, comparison);
    }

    internal static string ToEntryName(string path, bool windows)
    {
        var normalised = Normalise(path, windows);
        return windows ? "files/" + normalised.Remove(1, 1).Replace('\\', '/') : "files" + normalised;
    }

    internal static string LastSegment(string normalisedPath, bool windows)
    {
        var separator = SeparatorFor(windows);
        var trimmed = normalisedPath.TrimEnd(separator);
        var lastSeparator = trimmed.LastIndexOf(separator);
        return lastSeparator < 0 ? normalisedPath : trimmed[(lastSeparator + 1)..];
    }

    private static char SeparatorFor(bool windows)
    {
        return windows ? '\\' : '/';
    }

    private static string NormaliseWindows(string path, string normalised)
    {
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

    private static string NormaliseUnix(string path, string normalised)
    {
        if (normalised.Length > 1 && normalised.EndsWith('/'))
        {
            normalised = normalised.TrimEnd('/');
        }

        if (!normalised.StartsWith('/'))
        {
            throw new UksfException($"Backup path must be an absolute path: {path}", 400);
        }

        return normalised;
    }
}
