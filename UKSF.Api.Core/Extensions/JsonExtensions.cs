using System.Text.Json;
using System.Text.Json.Nodes;

namespace UKSF.Api.Core.Extensions;

public static class JsonExtensions
{
    extension(object source)
    {
        public T Copy<T>()
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, DefaultJsonSerializerOptions.Options), DefaultJsonSerializerOptions.Options);
        }
    }

    extension(string jsonString)
    {
        public string Escape()
        {
            return jsonString.Replace("\\", "\\\\");
        }
    }

    extension(JsonNode jsonNode)
    {
        public string GetValueFromObject(string key)
        {
            return jsonNode[key] is not null ? jsonNode[key].ToString() : string.Empty;
        }
    }
}
