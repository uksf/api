using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UKSF.Api.Admin.Models;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Admin.Extensions {
    public static class VariablesExtensions {
        public static VariableItem AssertHasItem(this VariableItem variableItem) {
            if (variableItem.item == null) {
                throw new Exception($"Variable {variableItem.key} has no item");
            }

            return variableItem;
        }

        public static string AsString(this VariableItem variable) => variable.AssertHasItem().item.ToString();

        public static double AsDouble(this VariableItem variable) {
            string item = variable.AsString();
            if (!double.TryParse(item, out double output)) {
                throw new InvalidCastException($"Variable item {item} cannot be converted to a double");
            }

            return output;
        }

        public static bool AsBool(this VariableItem variable) {
            string item = variable.AsString();
            if (!bool.TryParse(item, out bool output)) {
                throw new InvalidCastException($"Variable item {item} cannot be converted to a bool");
            }

            return output;
        }

        public static ulong AsUlong(this VariableItem variable) {
            string item = variable.AsString();
            if (!ulong.TryParse(item, out ulong output)) {
                throw new InvalidCastException($"Variable item {item} cannot be converted to a ulong");
            }

            return output;
        }

        public static string[] AsArray(this VariableItem variable, Func<string, string> predicate = null) {
            string itemString = variable.AsString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            string[] items = itemString.Split(",");
            return predicate != null ? items.Select(predicate).ToArray() : items;
        }

        public static IEnumerable<string> AsEnumerable(this VariableItem variable, Func<string, string> predicate = null) {
            string itemString = variable.AsString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            IEnumerable<string> items = itemString.Split(",").AsEnumerable();
            return predicate != null ? items.Select(predicate) : items;
        }

        public static IEnumerable<double> AsDoublesArray(this VariableItem variable) {
            string itemString = variable.AsString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            IEnumerable<double> items = itemString.Split(",").Select(x => x.ToDouble());
            return items;
        }
    }
}
