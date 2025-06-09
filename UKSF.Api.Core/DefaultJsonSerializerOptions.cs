using System.Text.Json;
using UKSF.Api.Core.Converters;

namespace UKSF.Api.Core;

public static class DefaultJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new InferredTypeConverter(), new DateTimeOffsetConverter() }
    };
}
