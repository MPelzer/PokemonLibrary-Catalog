namespace CatalogBuilder.Normalization;

/// <summary>
/// A correction of source prints in some sets (<c>config/pipeline.json</c>, <c>printFixes</c>): a print carrying
/// <see cref="Tag"/> gets <see cref="Finish"/> and, with <see cref="RemoveTag"/>, loses the tag. Only finish and tags
/// change – the print ID keeps the source key (FR-CAT-10). Example (catalog #5): EX reverse holos carry the set logo,
/// TCGdex lists the ex11–ex16 reverses as holo/normal + set logo stamp.
/// </summary>
public sealed record PrintFix(IReadOnlyList<string> Sets, string Tag, string? Finish = null, bool RemoveTag = false, string? Reason = null)
{
    /// <summary>The fixed key for a print of <paramref name="setCode"/> (normalized, i.e. language-independent tags).</summary>
    public PrintKey Apply(string setCode, PrintKey key) =>
        !Sets.Contains(setCode) || !key.Tags.Contains(Tag)
            ? key
            : new PrintKey(Finish ?? key.Finish, key.Edition, RemoveTag ? [.. key.Tags.Where(t => t != Tag)] : key.Tags);
}
