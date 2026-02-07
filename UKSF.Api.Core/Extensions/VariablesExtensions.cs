using System.Text.RegularExpressions;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Extensions;

public static class VariablesExtensions
{
    extension(DomainVariableItem variable)
    {
        public DomainVariableItem AssertHasItem()
        {
            if (variable.Item == null)
            {
                throw new Exception($"Variable {variable.Key} has no item");
            }

            return variable;
        }

        public string AsString()
        {
            return variable.AssertHasItem().Item.ToString();
        }

        public int AsInt()
        {
            var item = variable.AsString();
            if (!int.TryParse(item, out var output))
            {
                throw new InvalidCastException($"VariableItem {item} cannot be converted to an int");
            }

            return output;
        }

        public double AsDouble()
        {
            var item = variable.AsString();
            if (!double.TryParse(item, out var output))
            {
                throw new InvalidCastException($"VariableItem {item} cannot be converted to a double");
            }

            return output;
        }

        public bool AsBool()
        {
            var item = variable.AsString();
            if (!bool.TryParse(item, out var output))
            {
                throw new InvalidCastException($"VariableItem {item} cannot be converted to a bool");
            }

            return output;
        }

        public bool AsBoolWithDefault(bool defaultState)
        {
            if (variable?.Item == null)
            {
                return false;
            }

            var item = variable.Item.ToString();
            return !bool.TryParse(item, out var output) ? defaultState : output;
        }

        public ulong AsUlong()
        {
            var item = variable.AsString();
            if (!ulong.TryParse(item, out var output))
            {
                throw new InvalidCastException($"VariableItem {item} cannot be converted to a ulong");
            }

            return output;
        }

        public string[] AsArray(Func<string, string> predicate = null)
        {
            var itemString = variable.AsString();
            itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
            var items = itemString.Split(",");
            return predicate is not null ? items.Select(predicate).ToArray() : items;
        }

        public IEnumerable<string> AsEnumerable(Func<string, string> predicate = null)
        {
            var items = variable.AsArray();
            return predicate is not null ? items.Select(predicate) : items.AsEnumerable();
        }

        public IEnumerable<int> AsIntArray()
        {
            var items = variable.AsArray();
            return items.Select(x => x.ToInt());
        }

        public IEnumerable<double> AsDoublesArray()
        {
            var items = variable.AsArray();
            return items.Select(x => x.ToDouble());
        }
    }
}
