using System.Text.Json;
using System.Text.Json.Nodes;

namespace UKSF.Api.Core.Extensions;

public static class JsonExtensions
{
    public static T Copy<T>(this object source)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, DefaultJsonSerializerOptions.Options), DefaultJsonSerializerOptions.Options);
    }

    public static string Escape(this string jsonString)
    {
        return jsonString.Replace("\\", "\\\\");
    }

    public static string GetValueFromObject(this JsonNode jsonNode, string key)
    {
        return jsonNode[key] is not null ? jsonNode[key].ToString() : string.Empty;
    }
}
