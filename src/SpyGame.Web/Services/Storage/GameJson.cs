using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpyGame.Web.Services.Storage;

/// <summary>How rooms and sessions are written to the store (and to the browser).</summary>
public static class GameJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
