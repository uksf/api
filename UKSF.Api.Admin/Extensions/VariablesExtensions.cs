using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UKSF.Api.Admin.Models;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Admin.Extensions
{
    public static class VariablesExtensions
    {
        public static VariableItem AssertHasItem(this VariableItem variableItem)
        {
            if (variableItem.Item == null)
            {
                throw new($"Variable {variableItem.Key} has no item");
            }

            return variableItem;
        }

        public static string AsString(this VariableItem variable)
        {
            return variable.AssertHasItem().Item.ToString();
        }

        public static int AsInt(this VariableItem variable)
        {
            string item = variable.AsString();
            if (!int.TryParse(item, out int output))
            {
                throw new InvalidCastException($"VariableItem {item} cannot be converted to an int");
            }

            return output;
        }

        public static double AsDouble(this VariableItem variable)
        {
            string item = variable.AsString();
            if (!double.TryParse(item, out double output))
            {
                throw new InvalidCastException($"VariableItem {item} cannot be converted to a double");
            }

            return output;
        }

        public static bool AsBool(this VariableItem variable)
        {
            string item = variable.AsString();
            if (!bool.TryParse(item, out bool output))
            {
                throw new InvalidCastException($"VariableItem {item} cannot be converted to a bool");
            }

            return output;
        }

        public static bool AsBoolWithDefault(this VariableItem variable, bool defaultState)
        {
            if (variable?.Item == null)
            {
                return false;
            }

            string item = variable.Item.ToString();
            return !bool.TryParse(item, out bool output) ? defaultState : output;
        }

        public static ulong AsUlong(this VariableItem variable)
        {
            string item = variable.AsString();
            if (!ulong.TryParse(item, out ulong output))
            {
                throw new InvalidCastException($"VariableItem {item} cannot be converted to a ulong");
            }

            return output;
        }

        public static string[] AsArray(this VariableItem variable, Func<string, string> predicate = null)
        {
            string itemString = variable.AsString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            string[] items = itemString.Split(",");
            return predicate != null ? items.Select(predicate).ToArray() : items;
        }

        public static IEnumerable<string> AsEnumerable(this VariableItem variable, Func<string, string> predicate = null)
        {
            string itemString = variable.AsString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            IEnumerable<string> items = itemString.Split(",").AsEnumerable();
            return predicate != null ? items.Select(predicate) : items;
        }

        public static IEnumerable<int> AsIntArray(this VariableItem variable)
        {
            string itemString = variable.AsString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            IEnumerable<int> items = itemString.Split(",").Select(x => x.ToInt());
            return items;
        }

        public static IEnumerable<double> AsDoublesArray(this VariableItem variable)
        {
            string itemString = variable.AsString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            IEnumerable<double> items = itemString.Split(",").Select(x => x.ToDouble());
            return items;
        }
    }
}
