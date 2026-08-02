using System.Text.RegularExpressions;

namespace UKSF.Api.Backups.Services;

/// <summary>
///     Prepares a connection string for mongodump: the password moves to a config file so no process listing can read
///     it, and the database is cleared so the dump covers every database rather than the one the API happens to use.
/// </summary>
public static partial class MongoUriCleaner
{
    public static string ForDump(string uri)
    {
        return WithoutDatabase(WithoutPassword(uri));
    }

    private static string WithoutPassword(string uri)
    {
        return PasswordPattern().Replace(uri, "$1@");
    }

    private static string WithoutDatabase(string uri)
    {
        return DatabasePattern().Replace(uri, "$1/");
    }

    [GeneratedRegex(@"^(mongodb(?:\+srv)?://[^:/@]+):[^@]*@")]
    private static partial Regex PasswordPattern();

    /// <summary>Matches the path between the host list and the query string, if there is one.</summary>
    [GeneratedRegex(@"(@[^/?]+)/[^?]*")]
    private static partial Regex DatabasePattern();
}
