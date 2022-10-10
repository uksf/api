using System.Text.Json;
using UKSF.Api.Shared.Converters;

namespace UKSF.Api.Shared;

public static class DefaultJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new InferredTypeConverter(), new DateTimeOffsetConverter() }
    };
}
