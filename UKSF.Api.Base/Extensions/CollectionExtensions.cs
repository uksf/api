using System.Collections.Generic;

namespace UKSF.Api.Base.Extensions {
    public static class CollectionExtensions {
        public static void CleanHashset(this HashSet<string> collection) {
            collection.RemoveWhere(string.IsNullOrEmpty);
        }
    }
}
