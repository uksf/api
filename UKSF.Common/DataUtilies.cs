using System.Reflection;
using Newtonsoft.Json.Linq;

namespace UKSF.Common {
    public static class DataUtilies {
        public static string GetIdValue<T>(this T data) {
            FieldInfo id = data.GetType().GetField("id");
            if (id == null) return "";
            return id.GetValue(data) as string;
        }

        public static string GetValueFromBody(this JObject body, string key) => body[key] != null ? body[key].ToString() : string.Empty;
    }
}
