using System.Text.Json;
using System.Text.Json.Nodes;
using CatalogBuilder.Format;
using CatalogBuilder.Normalization;

namespace CatalogBuilder.Pipeline;

public sealed record PipelineSettings(
    IReadOnlyList<string> Languages,
    string ReferenceLanguage,
    IReadOnlyList<string> ExcludedSeries,
    int RequestConcurrency,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ValueMap);

/// <summary>Repository configuration: <c>config/pipeline.json</c>, <c>config/sources.json</c>, <c>config/vocabulary.json</c>.</summary>
public sealed class PipelineConfig
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private PipelineConfig(string repoRoot, PipelineSettings settings, IReadOnlyList<SourceInfo> sources, string vocabularyJson)
    {
        RepoRoot = repoRoot;
        Settings = settings;
        Sources = sources;
        VocabularyJson = vocabularyJson;
    }

    public string RepoRoot { get; }
    public PipelineSettings Settings { get; }
    public IReadOnlyList<SourceInfo> Sources { get; }
    public string VocabularyJson { get; }

    public string CatalogDir => Path.Combine(RepoRoot, "catalog");
    public string SchemaDir => Path.Combine(RepoRoot, "schema", "v1");
    public string DistDir => Path.Combine(RepoRoot, "dist");

    public Vocabulary CreateVocabulary() => new(JsonNode.Parse(VocabularyJson)!, Settings.ValueMap);

    public static PipelineConfig Load(string repoRoot)
    {
        var config = Path.Combine(repoRoot, "config");
        var settings = JsonSerializer.Deserialize<PipelineSettings>(File.ReadAllText(Path.Combine(config, "pipeline.json")), ReadOptions)
                       ?? throw new InvalidOperationException("config/pipeline.json is empty.");
        var sources = JsonSerializer.Deserialize<List<SourceInfo>>(File.ReadAllText(Path.Combine(config, "sources.json")), ReadOptions)
                      ?? throw new InvalidOperationException("config/sources.json is empty.");
        return new PipelineConfig(repoRoot, settings, sources, File.ReadAllText(Path.Combine(config, "vocabulary.json")));
    }

    /// <summary>Walks up from the working directory to the folder containing <c>config/pipeline.json</c>.</summary>
    public static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "config", "pipeline.json"))) return dir.FullName;
        throw new InvalidOperationException("Run the builder inside the catalog repository (config/pipeline.json not found).");
    }
}
