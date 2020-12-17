using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace UKSF.Api.Shared.Extensions {
    public static class ChangeUtilities {
        public static string Changes<T>(this T original, T updated) => DeepEquals(original, updated) ? "\tNo changes" : FormatChanges(GetChanges(original, updated));

        private static List<Change> GetChanges<T>(this T original, T updated) {
            List<Change> changes = new();
            Type type = original.GetType();
            IEnumerable<FieldInfo> fields = type.GetFields();

            if (!fields.Any()) {
                changes.Add(GetChange(type, type.Name.Split('`')[0], original, updated));
                return changes;
            }

            foreach (FieldInfo fieldInfo in fields) {
                string name = fieldInfo.Name;
                object originalValue = fieldInfo.GetValue(original);
                object updatedValue = fieldInfo.GetValue(updated);
                if (originalValue == null && updatedValue == null) continue;
                if (DeepEquals(originalValue, updatedValue)) continue;

                if (fieldInfo.FieldType.IsClass && !fieldInfo.FieldType.IsSerializable) {
                    changes.Add(new Change { Type = ChangeType.CLASS, Name = name, InnerChanges = GetChanges(originalValue, updatedValue) });
                } else {
                    changes.Add(GetChange(fieldInfo.FieldType, name, originalValue, updatedValue));
                }
            }

            return changes;
        }

        private static Change GetChange(Type type, string name, object original, object updated) {
            if (type != typeof(string) && updated is IEnumerable originalListValue && original is IEnumerable updatedListValue) {
                return new Change { Type = ChangeType.LIST, Name = name == string.Empty ? "List" : name, InnerChanges = GetListChanges(originalListValue, updatedListValue) };
            }

            if (original == null) {
                return new Change { Type = ChangeType.ADDITION, Name = name, Updated = updated.ToString() };
            }

            if (updated == null) {
                return new Change { Type = ChangeType.REMOVAL, Name = name, Original = original.ToString() };
            }

            return new Change { Type = ChangeType.CHANGE, Name = name, Original = original.ToString(), Updated = updated.ToString() };
        }

        private static List<Change> GetListChanges(this IEnumerable original, IEnumerable updated) {
            List<object> originalObjects = original == null ? new List<object>() : original.Cast<object>().ToList();
            List<object> updatedObjects = updated == null ? new List<object>() : updated.Cast<object>().ToList();
            List<Change> changes = originalObjects.Where(originalObject => !updatedObjects.Any(updatedObject => DeepEquals(originalObject, updatedObject))).Select(x => new Change { Type = ChangeType.ADDITION, Updated = x.ToString() }).ToList();
            changes.AddRange(updatedObjects.Where(updatedObject => !originalObjects.Any(originalObject => DeepEquals(originalObject, updatedObject))).Select(x => new Change { Type = ChangeType.REMOVAL, Original = x.ToString() }));
            return changes;
        }

        private static bool DeepEquals(object original, object updated) {
            if (original == null && updated == null) return true;
            if (original == null || updated == null) return false;

            JToken originalObject = JToken.FromObject(original);
            JToken updatedObject = JToken.FromObject(updated);
            return JToken.DeepEquals(originalObject, updatedObject);
        }

        private static string FormatChanges(IReadOnlyCollection<Change> changes, string indentation = "") {
            if (!changes.Any()) return "\tNo changes";

            return changes.OrderBy(x => x.Type)
                          .ThenBy(x => x.Name)
                          .Aggregate(
                              "",
                              (current, change) => current +
                                                   $"\n\t{indentation}'{change.Name}'" +
                                                   " " +
                                                   change.Type switch {
                                                       ChangeType.ADDITION => $"added as '{change.Updated}'",
                                                       ChangeType.REMOVAL  => $"as '{change.Original}' removed",
                                                       ChangeType.CLASS    => $"changed:{FormatChanges(change.InnerChanges, indentation + "\t")}",
                                                       ChangeType.LIST     => $"changed:{FormatListChanges(change.InnerChanges, indentation + "\t")}",
                                                       _                   => $"changed from '{change.Original}' to '{change.Updated}'"
                                                   }
                          );
        }

        private static string FormatListChanges(IEnumerable<Change> changes, string indentation = "") {
            string changesString = "";
            foreach (Change change in changes.OrderBy(x => x.Type).ThenBy(x => x.Name)) {
                if (change.Type == ChangeType.ADDITION) {
                    changesString += $"\n\t{indentation}added: '{change.Updated}'";
                } else if (change.Type == ChangeType.REMOVAL) {
                    changesString += $"\n\t{indentation}removed: '{change.Original}'";
                }
            }

            return changesString;
        }
    }

    public class Change {
        public List<Change> InnerChanges = new();
        public string Name;
        public string Original;
        public ChangeType Type;
        public string Updated;
    }

    public enum ChangeType {
        ADDITION,
        CHANGE,
        LIST,
        REMOVAL,
        CLASS
    }
}
