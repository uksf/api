using System.Text;
using System.Text.RegularExpressions;

namespace UKSF.Api.Backups.Services;

/// <summary>
///     Name patterns for selections that change on their own. Arma profile folders come and go with game server
///     entries, so the backup matches `*.Arma3Profile` rather than a list of paths that goes stale.
/// </summary>
public static class BackupGlob
{
    public static bool IsGlob(string pattern)
    {
        return pattern is not null && (pattern.Contains('*') || pattern.Contains('?'));
    }

    public static bool HasSeparator(string pattern)
    {
        return pattern is not null && (pattern.Contains('\\') || pattern.Contains('/'));
    }

    /// <summary>Matches the last segment of a path, so a pattern applies at any depth.</summary>
    public static bool MatchesName(string pattern, string path)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var name = path.TrimEnd('\\', '/').Split('\\', '/').Last();
        return ToRegex(pattern).IsMatch(name);
    }

    private static Regex ToRegex(string pattern)
    {
        var builder = new StringBuilder("^");

        foreach (var character in pattern)
        {
            builder.Append(
                character switch
                {
                    '*' => ".*",
                    '?' => ".",
                    _   => Regex.Escape(character.ToString())
                }
            );
        }

        return new Regex(builder.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
