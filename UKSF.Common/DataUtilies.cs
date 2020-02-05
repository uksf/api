using System.Reflection;

namespace UKSF.Common {
    public static class DataUtilies {
        public static string GetIdValue<T>(this T data) {
            FieldInfo id = data.GetType().GetField("id");
            if (id == null) return "";
            return id.GetValue(data) as string;
        }
    }
}
