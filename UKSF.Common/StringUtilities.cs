﻿using System.Globalization;
using System.Text.RegularExpressions;

namespace UKSF.Common {
    public static class StringUtilities {
        public static bool ContainsCaseInsensitive(this string text, string searchElement) => !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(searchElement) && text.ToUpper().Contains(searchElement.ToUpper());

        public static double ToDouble(this string text) => double.TryParse(text, out double number) ? number : 0d;

        public static string ToTitleCase(this string text) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);

        public static string Keyify(this string key) => key.Trim().ToUpper().Replace(" ", "_");

        public static string RemoveSpaces(this string item) => item.Replace(" ", string.Empty);

        public static string RemoveNewLines(this string item) => item.Replace("\\n", string.Empty);

        public static string RemoveQuotes(this string item) => item.Replace("\"", string.Empty);

        public static string RemoveEmbeddedQuotes(this string item) {
            Match match = new Regex("(\\\".*).+(.*?\\\")").Match(item);
            item = item.Remove(match.Index, match.Length).Insert(match.Index, match.ToString().Replace("\"\"", "'"));
            return Regex.Replace(item, "\\\"\\s+\\\"", string.Empty);
        }
    }
}