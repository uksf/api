using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.JsonDiffPatch;

namespace UKSF.Api.Core.Extensions;

public static class ChangeUtilities
{
    extension<T>(T original)
    {
        public string Changes(T updated)
        {
            return DeepEquals(original, updated) ? "\tNo changes" : FormatChanges(GetChanges(original, updated));
        }
    }

    private static List<Change> GetChanges<T>(T original, T updated)
    {
        List<Change> changes = [];
        var type = original.GetType();
        IEnumerable<PropertyInfo> properties = type.GetProperties();

        if (!properties.Any() || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
        {
            changes.Add(GetChange(type, type.Name.Split('`')[0], original, updated));
            return changes;
        }

        foreach (var propertyInfo in properties)
        {
            // Skip properties that require parameters (like indexers)
            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var name = propertyInfo.Name;
            var originalValue = propertyInfo.GetValue(original);
            var updatedValue = propertyInfo.GetValue(updated);
            if (originalValue == null && updatedValue == null)
            {
                continue;
            }

            if (DeepEquals(originalValue, updatedValue))
            {
                continue;
            }

            // Check if this property is a collection type (but not string)
            if (IsCollectionType(propertyInfo.PropertyType))
            {
                changes.Add(GetChange(propertyInfo.PropertyType, name, originalValue, updatedValue));
            }
            else if (propertyInfo.PropertyType.IsClass && !IsSimpleType(propertyInfo.PropertyType))
            {
                changes.Add(
                    new Change
                    {
                        Type = ChangeType.Class,
                        Name = name,
                        InnerChanges = GetChanges(originalValue, updatedValue)
                    }
                );
            }
            else
            {
                changes.Add(GetChange(propertyInfo.PropertyType, name, originalValue, updatedValue));
            }
        }

        return changes;
    }

    private static Change GetChange(Type type, string name, object original, object updated)
    {
        if (type != typeof(string) && original is IEnumerable originalListValue && updated is IEnumerable updatedListValue)
        {
            return new Change
            {
                Type = ChangeType.List,
                Name = name == string.Empty ? "List" : name,
                InnerChanges = GetListChanges(originalListValue, updatedListValue)
            };
        }

        if (original == null)
        {
            return new Change
            {
                Type = ChangeType.Addition,
                Name = name,
                Updated = updated.ToString()
            };
        }

        if (updated == null)
        {
            return new Change
            {
                Type = ChangeType.Removal,
                Name = name,
                Original = original.ToString()
            };
        }

        return new Change
        {
            Type = ChangeType.Change,
            Name = name,
            Original = original.ToString(),
            Updated = updated.ToString()
        };
    }

    private static List<Change> GetListChanges(IEnumerable original, IEnumerable updated)
    {
        var originalObjects = original == null ? [] : original.Cast<object>().ToList();
        var updatedObjects = updated == null ? [] : updated.Cast<object>().ToList();
        var changes = updatedObjects.Where(updatedObject => !originalObjects.Any(originalObject => DeepEquals(originalObject, updatedObject)))
                                    .Select(x => new Change { Type = ChangeType.Addition, Updated = x.ToString() })
                                    .ToList();
        changes.AddRange(
            originalObjects.Where(originalObject => !updatedObjects.Any(updatedObject => DeepEquals(originalObject, updatedObject)))
                           .Select(x => new Change { Type = ChangeType.Removal, Original = x.ToString() })
        );
        return changes;
    }

    private static bool DeepEquals(object original, object updated)
    {
        if (original == null && updated == null)
        {
            return true;
        }

        if (original == null || updated == null)
        {
            return false;
        }

        var originalObject = JsonDocument.Parse(JsonSerializer.Serialize(original, DefaultJsonSerializerOptions.Options));
        var updatedObject = JsonDocument.Parse(JsonSerializer.Serialize(updated, DefaultJsonSerializerOptions.Options));
        return originalObject.DeepEquals(updatedObject);
    }

    private static bool IsCollectionType(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateOnly) ||
               type == typeof(TimeOnly) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && IsSimpleType(Nullable.GetUnderlyingType(type)!));
    }

    private static string FormatChanges(IReadOnlyCollection<Change> changes, string indentation = "")
    {
        if (!changes.Any())
        {
            return "\tNo changes";
        }

        return changes.OrderBy(x => x.Type)
                      .ThenBy(x => x.Name)
                      .Aggregate(
                          "",
                          (current, change) => current +
                                               $"\n\t{indentation}'{change.Name}'" +
                                               " " +
                                               change.Type switch
                                               {
                                                   ChangeType.Addition => $"added as '{change.Updated}'",
                                                   ChangeType.Removal  => $"as '{change.Original}' removed",
                                                   ChangeType.Class    => $"changed:{FormatChanges(change.InnerChanges, indentation + "\t")}",
                                                   ChangeType.List     => $"changed:{FormatListChanges(change.InnerChanges, indentation + "\t")}",
                                                   _                   => $"changed from '{change.Original}' to '{change.Updated}'"
                                               }
                      );
    }

    private static string FormatListChanges(IEnumerable<Change> changes, string indentation = "")
    {
        var changesString = "";
        foreach (var change in changes.OrderBy(x => x.Type).ThenBy(x => x.Name))
        {
            if (change.Type == ChangeType.Addition)
            {
                changesString += $"\n\t{indentation}added: '{change.Updated}'";
            }
            else if (change.Type == ChangeType.Removal)
            {
                changesString += $"\n\t{indentation}removed: '{change.Original}'";
            }
        }

        return changesString;
    }
}

public class Change
{
    public List<Change> InnerChanges = new();
    public string Name;
    public string Original;
    public ChangeType Type;
    public string Updated;
}

public enum ChangeType
{
    Addition,
    Change,
    List,
    Removal,
    Class
}
