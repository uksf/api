using System.Collections.Generic;

namespace UKSF.Common {
    public static class CollectionUtilities {
        public static void CleanHashset(this HashSet<string> collection) {
            collection.RemoveWhere(string.IsNullOrEmpty);
        }
    }
}
