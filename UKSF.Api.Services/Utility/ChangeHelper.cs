using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json.Linq;

namespace UKSF.Api.Services.Utility {
    public static class ChangeHelper {
        public static string Changes<T>(this T original, T updated) {
            List<FieldInfo> fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(x => !x.IsDefined(typeof(BsonIgnoreAttribute))).ToList();
            IEnumerable<Change> changes = FindChanges(JToken.FromObject(original), JToken.FromObject(updated), fields);
            return changes.Aggregate(
                string.Empty,
                (a, b) => {
                    if (b.Original == null && b.Updated != null) {
                        return $"{a}\n\t{b.Name} added: '{b.Updated}'";
                    }

                    if (b.Original != null && b.Updated == null) {
                        return $"{a}\n\t{b.Name} removed: '{b.Original}'";
                    }

//                        if (b.Original is IEnumerable && b.Updated is IEnumerable) {
//                            string listChanges = ((IEnumerable<string>) b.Original).Select(x => x.ToString()).Changes(((IEnumerable<string>) b.Updated).Select(x => x.ToString()));
//                            return string.IsNullOrEmpty(listChanges) ? string.Empty : $"{a}\n\t{b.Name} changed:\n{listChanges}";
//                        }

                    return !Equals(b.Original, b.Updated) ? $"{a}\n\t'{b.Name}' changed: '{b.Original}' to '{b.Updated}'" : "";
                }
            );
        }

        public static string Changes(this IEnumerable<string> original, IEnumerable<string> updated) {
            StringBuilder changes = new StringBuilder();
            List<string> updatedList = updated.ToList();
            foreach (string addition in updatedList.Where(x => !original.Contains(x))) {
                changes.Append($"\n\tAdded: '{addition}'");
            }

            foreach (string removal in original.Where(x => !updatedList.Contains(x))) {
                changes.Append($"\n\tRemoved: '{removal}'");
            }

            return changes.ToString();
        }

//        private static IEnumerable<Change> FindChanges<T>(this T original, T updated) {
//            IEnumerable<FieldInfo> fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(x => !x.IsDefined(typeof(BsonIgnoreAttribute)));
//            return fields.Select(fieldInfo => new {fieldInfo, originalValue = fieldInfo.GetValue(original), updatedValue = fieldInfo.GetValue(updated)})
//                         .Where(x => !Equals(x.originalValue, x.updatedValue))
//                         .Select(x => new Change {Name = x.fieldInfo.Name, Original = x.originalValue, Updated = x.updatedValue})
//                         .ToList();
//        }

        private static IEnumerable<Change> FindChanges(this JToken original, JToken updated, IReadOnlyCollection<FieldInfo> allowedFields) {
            List<Change> changes = new List<Change>();
            if (JToken.DeepEquals(original, updated)) return changes;

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (original.Type == JTokenType.Object) {
                JObject originalObject = original as JObject;
                JObject updatedObject = updated as JObject;

                if (originalObject == null) {
                    originalObject = new JObject();
                }

                if (updatedObject == null) {
                    updatedObject = new JObject();
                }

                List<string> added = updatedObject.Properties().Select(c => c.Name).Except(originalObject.Properties().Select(c => c.Name)).ToList();
                List<string> removed = originalObject.Properties().Select(c => c.Name).Except(updatedObject.Properties().Select(c => c.Name)).ToList();
                List<string> unchanged = originalObject.Properties().Where(c => JToken.DeepEquals(c.Value, updated[c.Name])).Select(c => c.Name).ToList();
                List<string> changed = originalObject.Properties().Select(c => c.Name).Except(added).Except(unchanged).ToList();

                changes.AddRange(added.Where(x => allowedFields.Any(y => y.Name == x)).Select(key => updatedObject.Properties().First(x => x.Name == key)).Select(addedObject => new Change {Name = addedObject.Name, Original = null, Updated = addedObject.Value.Value<string>()}));
                changes.AddRange(removed.Where(x => allowedFields.Any(y => y.Name == x)).Select(key => originalObject.Properties().First(x => x.Name == key)).Select(removedObject => new Change {Name = removedObject.Name, Original = removedObject.Value.Value<string>(), Updated = null}));

                foreach (string key in changed.Where(x => allowedFields.Any(y => y.Name == x))) {
                    JToken originalChangedObject = originalObject[key];
                    JToken updatedChangedObject = updatedObject[key];
                    changes.AddRange(FindChanges(originalChangedObject, updatedChangedObject, allowedFields));
                }
            } else {
                changes.Add(new Change {Name = ((JProperty) updated.Parent).Name, Original = original.Value<string>(), Updated = updated.Value<string>()});
            }

            return changes;
        }
    }

    public class Change {
        public string Name;
        public string Original;
        public string Updated;
    }
}
