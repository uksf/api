using Newtonsoft.Json;

namespace UKSF.Common {
    public static class JsonUtilities {
        public static T Copy<T>(this object source) {
            JsonSerializerSettings deserializeSettings = new JsonSerializerSettings {ObjectCreationHandling = ObjectCreationHandling.Replace};
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
        }
    }
}
