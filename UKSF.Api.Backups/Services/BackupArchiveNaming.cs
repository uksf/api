using System.Globalization;
using System.Text.RegularExpressions;

namespace UKSF.Api.Backups.Services;

public static partial class BackupArchiveNaming
{
    private const string Prefix = "uksf-backup-";
    private const string Suffix = ".zip.enc";
    private const string Stamp = "yyyyMMdd-HHmmss";

    public static string ForTime(DateTime utcNow)
    {
        return $"{Prefix}{utcNow.ToString(Stamp, CultureInfo.InvariantCulture)}{Suffix}";
    }

    public static bool IsArchive(string pathOrName)
    {
        return ArchivePattern().IsMatch(NameOf(pathOrName));
    }

    /// <summary>Sorts by the stamp in the name, so a copied file with a fresh write time cannot jump the queue.</summary>
    public static DateTime SortKey(string pathOrName)
    {
        var match = ArchivePattern().Match(NameOf(pathOrName));
        if (!match.Success)
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParseExact(
            match.Groups[1].Value,
            Stamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed
        )
            ? parsed
            : DateTime.MinValue;
    }

    private static string NameOf(string pathOrName)
    {
        return pathOrName?.Split('\\', '/').Last() ?? string.Empty;
    }

    [GeneratedRegex(@"^uksf-backup-(\d{8}-\d{6})\.zip\.enc$", RegexOptions.IgnoreCase)]
    private static partial Regex ArchivePattern();
}
