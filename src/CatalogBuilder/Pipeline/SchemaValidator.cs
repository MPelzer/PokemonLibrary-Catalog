using System.Text.Json;
using Json.Schema;

namespace CatalogBuilder.Pipeline;

/// <summary>Validates generated files against the catalog format JSON Schema (the contract with the app).</summary>
public sealed class SchemaValidator
{
    private static readonly string[] Kinds = ["manifest", "vocabulary", "species", "set", "prices"];
    private readonly Dictionary<string, JsonSchema> _schemas;

    public SchemaValidator(string schemaDir)
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        options.SchemaRegistry.Register(JsonSchema.FromFile(Path.Combine(schemaDir, "common.schema.json"), options));
        _schemas = Kinds.ToDictionary(k => k, k => JsonSchema.FromFile(Path.Combine(schemaDir, $"{k}.schema.json"), options));
    }

    public static string KindOf(string relativePath) => relativePath switch
    {
        "manifest.json" or "vocabulary.json" or "species.json" => relativePath[..^".json".Length],
        _ when relativePath.StartsWith("sets/", StringComparison.Ordinal) => "set",
        _ when relativePath.StartsWith("prices/", StringComparison.Ordinal) => "prices",
        _ => throw new InvalidOperationException($"Unknown catalog file kind: {relativePath}"),
    };

    /// <summary>Returns error messages (empty when valid).</summary>
    public IReadOnlyList<string> Validate(string kind, string json)
    {
        using var document = JsonDocument.Parse(json);
        var results = _schemas[kind].Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid) return [];
        return [.. (results.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Take(20)];
    }

    /// <summary>Validates every JSON file below <paramref name="catalogDir"/>; returns "path: error" lines.</summary>
    public IReadOnlyList<string> ValidateDirectory(string catalogDir) =>
        [.. Directory.EnumerateFiles(catalogDir, "*.json", SearchOption.AllDirectories)
            .Select(f => (Rel: Path.GetRelativePath(catalogDir, f).Replace('\\', '/'), Full: f))
            .OrderBy(f => f.Rel, StringComparer.Ordinal)
            .SelectMany(f => Validate(KindOf(f.Rel), File.ReadAllText(f.Full)).Select(e => $"{f.Rel}: {e}"))];
}
