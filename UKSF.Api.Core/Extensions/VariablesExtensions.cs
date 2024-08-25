using System.Text.RegularExpressions;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Extensions;

public static class VariablesExtensions
{
    public static DomainVariableItem AssertHasItem(this DomainVariableItem variableItem)
    {
        if (variableItem.Item == null)
        {
            throw new Exception($"Variable {variableItem.Key} has no item");
        }

        return variableItem;
    }

    public static string AsString(this DomainVariableItem variable)
    {
        return variable.AssertHasItem().Item.ToString();
    }

    public static int AsInt(this DomainVariableItem variable)
    {
        var item = variable.AsString();
        if (!int.TryParse(item, out var output))
        {
            throw new InvalidCastException($"VariableItem {item} cannot be converted to an int");
        }

        return output;
    }

    public static double AsDouble(this DomainVariableItem variable)
    {
        var item = variable.AsString();
        if (!double.TryParse(item, out var output))
        {
            throw new InvalidCastException($"VariableItem {item} cannot be converted to a double");
        }

        return output;
    }

    public static bool AsBool(this DomainVariableItem variable)
    {
        var item = variable.AsString();
        if (!bool.TryParse(item, out var output))
        {
            throw new InvalidCastException($"VariableItem {item} cannot be converted to a bool");
        }

        return output;
    }

    public static bool AsBoolWithDefault(this DomainVariableItem variable, bool defaultState)
    {
        if (variable?.Item == null)
        {
            return false;
        }

        var item = variable.Item.ToString();
        return !bool.TryParse(item, out var output) ? defaultState : output;
    }

    public static ulong AsUlong(this DomainVariableItem variable)
    {
        var item = variable.AsString();
        if (!ulong.TryParse(item, out var output))
        {
            throw new InvalidCastException($"VariableItem {item} cannot be converted to a ulong");
        }

        return output;
    }

    public static string[] AsArray(this DomainVariableItem variable, Func<string, string> predicate = null)
    {
        var itemString = variable.AsString();
        itemString = Regex.Replace(itemString, "\\s*,\\s*", ",");
        var items = itemString.Split(",");
        return predicate is not null ? items.Select(predicate).ToArray() : items;
    }

    public static IEnumerable<string> AsEnumerable(this DomainVariableItem variable, Func<string, string> predicate = null)
    {
        var items = variable.AsArray();
        return predicate is not null ? items.Select(predicate) : items.AsEnumerable();
    }

    public static IEnumerable<int> AsIntArray(this DomainVariableItem variable)
    {
        var items = variable.AsArray();
        return items.Select(x => x.ToInt());
    }

    public static IEnumerable<double> AsDoublesArray(this DomainVariableItem variable)
    {
        var items = variable.AsArray();
        return items.Select(x => x.ToDouble());
    }
}
