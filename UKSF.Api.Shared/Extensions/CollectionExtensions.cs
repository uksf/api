using System.Collections.Generic;
using System.Linq;

namespace UKSF.Api.Shared.Extensions
{
    public static class CollectionExtensions
    {
        public static void CleanHashset(this HashSet<string> collection)
        {
            collection.RemoveWhere(string.IsNullOrEmpty);
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection)
        {
            return collection == null || collection.IsEmpty();
        }

        public static bool IsEmpty<T>(this IEnumerable<T> collection)
        {
            return !collection.Any();
        }

        public static TKey GetKeyFromValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TValue value)
        {
            return dictionary.FirstOrDefault(x => x.Value.Equals(value)).Key;
        }
    }
}
