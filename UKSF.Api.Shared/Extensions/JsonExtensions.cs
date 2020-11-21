using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UKSF.Api.Shared.Extensions {
    public static class JsonExtensions {
        public static T Copy<T>(this object source) {
            JsonSerializerSettings deserializeSettings = new() { ObjectCreationHandling = ObjectCreationHandling.Replace };
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        public static string Escape(this string jsonString) => jsonString.Replace("\\", "\\\\");

        public static string GetValueFromBody(this JObject body, string key) => body[key] != null ? body[key].ToString() : string.Empty;
    }
}
