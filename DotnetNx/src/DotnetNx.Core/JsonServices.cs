using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetNx.Core;

public static class JsonServices
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
