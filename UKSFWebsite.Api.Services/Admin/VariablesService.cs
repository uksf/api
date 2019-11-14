using System;
using System.Linq;
using System.Text.RegularExpressions;
using UKSFWebsite.Api.Models.Admin;

namespace UKSFWebsite.Api.Services.Admin {
    public static class VariablesService {
        public static string AsString(this VariableItem variable) => variable?.item.ToString();
        public static bool AsBool(this VariableItem variable) => bool.Parse(variable?.item.ToString() ?? throw new Exception("Variable does not exist"));
        public static ulong AsUlong(this VariableItem variable) => ulong.Parse(variable?.item.ToString() ?? throw new Exception("Variable does not exist"));

        public static string[] AsArray(this VariableItem variable, Func<string, string> predicate = null) {
            if (variable == null) {
                throw new Exception("Variable does not exist");
            }

            string itemString = variable.item.ToString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            string[] items = itemString.Split(",");
            return predicate != null ? items.Select(predicate).ToArray() : items;
        }
    }
}
