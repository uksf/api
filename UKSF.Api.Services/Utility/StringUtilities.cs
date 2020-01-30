using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Services.Utility {
    public static class StringUtilities {
        public static double ToDouble(this string text) => double.Parse(text);
        public static string ToTitleCase(string text) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
        public static string Keyify(this string key) => key.ToUpper().Replace(" ", "_");
        public static string RemoveSpaces(this string item) => item.Replace(" ", string.Empty);
        public static string RemoveNewLines(this string item) => item.Replace("\\n", string.Empty);
        public static string RemoveQuotes(this string item) => item.Replace("\"", string.Empty);
        public static bool ContainsCaseInsensitive(this string text, string element) => text.ToUpper().Contains(element.ToUpper());

        public static string RemoveEmbeddedQuotes(this string item) {
            Match match = new Regex("(\\\".*).+(.*?\\\")").Match(item);
            item = item.Remove(match.Index, match.Length).Insert(match.Index, match.ToString().Replace("\"\"", "'"));
            return Regex.Replace(item, "\\\"\\s+\\\"", string.Empty);
        }

        public static string ConvertObjectIds(this string message) {
            string newMessage = message;
            if (!string.IsNullOrEmpty(message)) {
                IDisplayNameService displayNameService = ServiceWrapper.ServiceProvider.GetService<IDisplayNameService>();
                IUnitsService unitsService = ServiceWrapper.ServiceProvider.GetService<IUnitsService>();
                IEnumerable<string> parts = Regex.Split(message, @"\s+").Where(s => s != string.Empty);
                foreach (string part in parts) {
                    if (ObjectId.TryParse(part, out ObjectId _)) {
                        string displayName = displayNameService.GetDisplayName(part);
                        if (displayName == part) {
                            Unit unit = unitsService.Data().GetSingle(x => x.id == part);
                            if (unit != null) {
                                displayName = unit.name;
                            }
                        }

                        newMessage = newMessage.Replace(part, displayName);
                    }
                }
            }

            return newMessage;
        }
    }
}
