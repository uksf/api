using Newtonsoft.Json.Linq;

namespace UKSF.Common {
    public static class DataUtilies {
        public static string GetValueFromBody(this JObject body, string key) => body[key] != null ? body[key].ToString() : string.Empty;
    }
}
