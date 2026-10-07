using System.Globalization;
using System.Text.RegularExpressions;
using MongoDB.Bson;

namespace UKSF.Api.Core.Extensions;

public static class StringExtensions
{
    extension(string text)
    {
        public bool ContainsIgnoreCase(string searchElement)
        {
            return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(searchElement) && text.ToUpper().Contains(searchElement.ToUpper());
        }

        public bool EqualsIgnoreCase(string otherText)
        {
            return string.Equals(text, otherText, StringComparison.InvariantCulture);
        }

        public double ToDouble()
        {
            return double.TryParse(text, out var number) ? number : 0d;
        }

        public int ToInt()
        {
            return int.TryParse(text, out var number) ? number : 0;
        }

        public string ToTitleCase()
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
        }

        public string Keyify()
        {
            return text.Trim().ToUpper().Replace(" ", "_");
        }

        public string RemoveSpaces()
        {
            return text.Replace(" ", string.Empty);
        }

        public string RemoveTrailingNewLineGroup()
        {
            return Regex.Replace(text, "\\s\\\\n\\s\"\";", ";");
        }

        public string RemoveNewLines()
        {
            return Regex.Replace(text, "\"[^\"]*\"|\\\\n", match => match.Value == "\\n" ? string.Empty : match.Value);
        }

        public string RemoveQuotes()
        {
            return text.Replace("\"", string.Empty);
        }

        public string RemoveEmbeddedQuotes()
        {
            var item = text;
            var match = new Regex("(\".*).+(.*?\")").Match(item);
            item = item.Remove(match.Index, match.Length).Insert(match.Index, match.ToString().Replace("\"\"", "'"));
            return Regex.Replace(item, "\"\\s+\"", string.Empty);
        }

        public IEnumerable<string> ExtractObjectIds()
        {
            return Regex.Matches(text, "(?<!\\$)[{(]?[0-9a-fA-F]{24}[)}]?").Where(x => x.Value.IsObjectId()).Select(x => x.Value);
        }

        public string EscapeForLogging()
        {
            return Regex.Match(text, "[0-9a-fA-F]{24}").Success ? $"${text}" : text;
        }

        public string UnescapeForLogging()
        {
            return Regex.Replace(text, "\\$([0-9a-fA-F]{24})", "$1");
        }

        public string TruncateObjectIds()
        {
            return Regex.Replace(text, "[{(]?([0-9a-fA-F]{4})([0-9a-fA-F]{16})([0-9a-fA-F]{4})[)}]?", "$1...$3");
        }

        public bool IsObjectId()
        {
            return ObjectId.TryParse(text, out _);
        }
    }
}
