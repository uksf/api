using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace UKSF.Api.Shared.Extensions {
    public static class ChangeUtilities {
        public static string Changes<T>(this T original, T updated) => DeepEquals(original, updated) ? "No changes" : FormatChanges(GetChanges(original, updated));

        private static List<Change> GetChanges<T>(this T original, T updated) {
            List<Change> changes = new List<Change>();
            Type type = original.GetType();
            foreach (FieldInfo fieldInfo in type.GetFields()) {
                string name = fieldInfo.Name;
                object originalValue = fieldInfo.GetValue(original);
                object updatedValue = fieldInfo.GetValue(updated);
                if (originalValue == null && updatedValue == null) continue;
                if (DeepEquals(originalValue, updatedValue)) continue;

                if (fieldInfo.FieldType.IsClass && !fieldInfo.FieldType.IsSerializable) {
                    // Class, recurse
                    changes.Add(new Change { Type = ChangeType.CLASS, Name = name, InnerChanges = GetChanges(originalValue, updatedValue) });
                } else if (fieldInfo.FieldType != typeof(string) && updatedValue is IEnumerable originalListValue && originalValue is IEnumerable updatedListValue) {
                    // List, get list changes
                    changes.Add(new Change { Type = ChangeType.LIST, Name = name, InnerChanges = GetListChanges(originalListValue, updatedListValue) });
                } else {
                    // Assume otherwise normal field
                    if (originalValue == null) {
                        changes.Add(new Change { Type = ChangeType.ADDITION, Name = name, Updated = updatedValue.ToString() });
                    } else if (updatedValue == null) {
                        changes.Add(new Change { Type = ChangeType.REMOVAL, Name = name, Original = originalValue.ToString() });
                    } else {
                        changes.Add(new Change { Type = ChangeType.CHANGE, Name = name, Original = originalValue.ToString(), Updated = updatedValue.ToString() });
                    }
                }
            }

            return changes;
        }

        private static List<Change> GetListChanges(this IEnumerable original, IEnumerable updated) {
            List<object> originalObjects = original == null ? new List<object>() : original.Cast<object>().ToList();
            List<object> updatedObjects = updated == null ? new List<object>() : updated.Cast<object>().ToList();
            List<Change> changes = originalObjects.Where(x => !updatedObjects.Contains(x)).Select(x => new Change { Type = ChangeType.ADDITION, Updated = x.ToString() }).ToList();
            changes.AddRange(updatedObjects.Where(x => !originalObjects.Contains(x)).Select(x => new Change { Type = ChangeType.REMOVAL, Original = x.ToString() }));
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
            if (!changes.Any()) return "No changes";

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
        public List<Change> InnerChanges = new List<Change>();
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
