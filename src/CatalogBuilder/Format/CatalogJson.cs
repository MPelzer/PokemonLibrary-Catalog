using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatalogBuilder.Format;

public static class CatalogJson
{
    public const string FormatVersion = "1.0";

    /// <summary>Indented, camelCase, nulls omitted, non-ASCII kept readable (diff-friendly in Git).</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options) + "\n";

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
