using Newtonsoft.Json;

namespace UKSF.Common {
    public static class JsonUtilities {
        public static T Copy<T>(this object source) {
            JsonSerializerSettings deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        public static string Escape(this string jsonString) => jsonString.Replace("\\", "\\\\"); // .Replace(@"\b", "\\\\b").Replace(@"\t", "\\\\t").Replace(@"\f", "\\\\f").Replace(@"\r", "\\\\r");
    }
}
