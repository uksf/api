using System.Globalization;
using System.Text.RegularExpressions;
using MongoDB.Bson;

namespace UKSF.Api.Core.Extensions;

public static class StringExtensions
{
    public static bool ContainsIgnoreCase(this string text, string searchElement)
    {
        return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(searchElement) && text.ToUpper().Contains(searchElement.ToUpper());
    }

    public static bool EqualsIgnoreCase(this string text, string otherText)
    {
        return string.Equals(text, otherText, StringComparison.InvariantCulture);
    }

    public static double ToDouble(this string text)
    {
        return double.TryParse(text, out var number) ? number : 0d;
    }

    public static int ToInt(this string text)
    {
        return int.TryParse(text, out var number) ? number : 0;
    }

    public static string ToTitleCase(this string text)
    {
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
    }

    public static string Keyify(this string key)
    {
        return key.Trim().ToUpper().Replace(" ", "_");
    }

    public static string RemoveSpaces(this string item)
    {
        return item.Replace(" ", string.Empty);
    }

    public static string RemoveTrailingNewLineGroup(this string item)
    {
        return Regex.Replace(item, "\\s\\\\n\\s\"\";", ";");
    }

    public static string RemoveNewLines(this string item)
    {
        return item.Replace("\\n", string.Empty);
    }

    public static string RemoveQuotes(this string item)
    {
        return item.Replace("\"", string.Empty);
    }

    public static string RemoveEmbeddedQuotes(this string item)
    {
        var match = new Regex("(\".*).+(.*?\")").Match(item);
        item = item.Remove(match.Index, match.Length).Insert(match.Index, match.ToString().Replace("\"\"", "'"));
        return Regex.Replace(item, "\"\\s+\"", string.Empty);
    }

    public static IEnumerable<string> ExtractObjectIds(this string text)
    {
        return Regex.Matches(text, "(?<!\\$)[{(]?[0-9a-fA-F]{24}[)}]?").Where(x => IsObjectId(x.Value)).Select(x => x.Value);
    }

    /// <summary>
    ///     Escapes an ID with a $ for logging the raw ID
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Escaped ID</returns>
    public static string EscapeForLogging(this string id)
    {
        return Regex.Match(id, "[0-9a-fA-F]{24}").Success ? $"${id}" : id;
    }

    /// <summary>
    ///     Removes the $ for escaping IDs in logs
    /// </summary>
    /// <param name="text"></param>
    /// <returns>Unescaped text</returns>
    public static string UnescapeForLogging(this string text)
    {
        return Regex.Replace(text, "\\$([0-9a-fA-F]{24})", "$1");
    }

    public static string TruncateObjectIds(this string text)
    {
        return Regex.Replace(text, "[{(]?([0-9a-fA-F]{4})([0-9a-fA-F]{16})([0-9a-fA-F]{4})[)}]?", "$1...$3");
    }

    public static bool IsObjectId(this string text)
    {
        return ObjectId.TryParse(text, out _);
    }
}
