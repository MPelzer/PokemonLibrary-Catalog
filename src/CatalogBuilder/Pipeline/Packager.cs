using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using CatalogBuilder.Format;

namespace CatalogBuilder.Pipeline;

/// <summary>Writes <c>catalog/manifest.json</c> and packs <c>dist/catalog-&lt;version&gt;.zip</c> + <c>.sha256</c> (FR-CAT-13).</summary>
public sealed class Packager(PipelineConfig config, TextWriter log)
{
    public static string DefaultVersion() => DateTime.UtcNow.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) + ".1";

    public int Run(string catalogVersion)
    {
        var catalogDir = config.CatalogDir;
        if (Directory.Exists(Path.Combine(catalogDir, "prices")))
        {
            // The public catalog must not redistribute marketplace prices (D27).
            log.WriteLine("FAILED: catalog/prices/ exists – prices must not be published. Delete it and package again.");
            return 4;
        }
        var files = Directory.EnumerateFiles(catalogDir, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(catalogDir, f).Replace('\\', '/'))
            .Where(p => p != "manifest.json")
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => new ManifestFile(p, SchemaValidator.KindOf(p), Sha256(Path.Combine(catalogDir, p))) { SetId = SetIdOf(p) })
            .ToList();
        var languages = files.Where(f => f.SetId is not null).Select(f => f.SetId![..2]).Distinct().Order().ToList();

        var manifest = new Manifest(CatalogJson.FormatVersion, catalogVersion,
            DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture), languages, config.Sources, files);
        var manifestJson = CatalogJson.Serialize(manifest);
        var errors = new SchemaValidator(config.SchemaDir).Validate("manifest", manifestJson);
        if (errors.Count > 0)
        {
            foreach (var e in errors) log.WriteLine("  manifest: " + e);
            return 2;
        }
        File.WriteAllText(Path.Combine(catalogDir, "manifest.json"), manifestJson);

        Directory.CreateDirectory(config.DistDir);
        var zipName = $"catalog-{catalogVersion}.zip";
        var zipPath = Path.Combine(config.DistDir, zipName);
        File.Delete(zipPath);
        ZipFile.CreateFromDirectory(catalogDir, zipPath, CompressionLevel.SmallestSize, includeBaseDirectory: false);
        File.WriteAllText(zipPath + ".sha256", $"{Sha256(zipPath)}  {zipName}\n");

        log.WriteLine($"Packed {files.Count} files → dist/{zipName} ({new FileInfo(zipPath).Length / 1048576.0:F1} MB)");
        return 0;
    }

    private static string? SetIdOf(string path)
    {
        var parts = path.Split('/');
        return parts.Length == 3 && parts[0] == "sets" ? $"{parts[1]}/{parts[2][..^".json".Length]}" : null;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
