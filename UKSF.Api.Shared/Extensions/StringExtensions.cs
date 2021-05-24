using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MongoDB.Bson;

namespace UKSF.Api.Shared.Extensions
{
    public static class StringExtensions
    {
        public static bool ContainsIgnoreCase(this string text, string searchElement)
        {
            return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(searchElement) && text.ToUpper().Contains(searchElement.ToUpper());
        }

        public static double ToDouble(this string text)
        {
            return double.TryParse(text, out double number) ? number : 0d;
        }

        public static int ToInt(this string text)
        {
            return int.TryParse(text, out int number) ? number : 0;
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
            Match match = new Regex("(\\\".*).+(.*?\\\")").Match(item);
            item = item.Remove(match.Index, match.Length).Insert(match.Index, match.ToString().Replace("\"\"", "'"));
            return Regex.Replace(item, "\\\"\\s+\\\"", string.Empty);
        }

        public static IEnumerable<string> ExtractObjectIds(this string text)
        {
            return Regex.Matches(text, @"[{(]?[0-9a-fA-F]{24}[)}]?").Where(x => IsObjectId(x.Value)).Select(x => x.Value);
        }

        public static bool IsObjectId(this string text)
        {
            return ObjectId.TryParse(text, out ObjectId unused);
        }
    }
}
