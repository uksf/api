namespace UKSF.Api.Core.Extensions;

public static class CollectionExtensions
{
    extension(HashSet<string> collection)
    {
        public void CleanHashset()
        {
            collection.RemoveWhere(string.IsNullOrEmpty);
        }
    }

    extension<T>(IEnumerable<T> collection)
    {
        public bool IsNullOrEmpty()
        {
            return collection == null || collection.IsEmpty();
        }

        public bool IsEmpty()
        {
            return !collection.Any();
        }
    }

    extension<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
    {
        public TKey GetKeyFromValue(TValue value)
        {
            return dictionary.FirstOrDefault(x => x.Value.Equals(value)).Key;
        }
    }
}
