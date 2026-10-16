using CatalogBuilder.Format;

namespace CatalogBuilder.Pipeline;

/// <summary>Warnings about prints of a built set – reported in the build log, never written to the catalog.</summary>
public static class PrintChecks
{
    /// <summary>
    /// Prints of a card with the same finish, edition and tags after normalization (e.g. a localized and an untranslated
    /// tag that now mean the same) – the app could not tell them apart.
    /// </summary>
    public static IEnumerable<string> Duplicates(IEnumerable<CatalogCard> cards) =>
        cards.SelectMany(card => card.Prints
            .GroupBy(p => $"{p.Finish}.{p.Edition}~{string.Join('~', p.Tags ?? [])}")
            .Where(g => g.Count() > 1)
            .Select(g => $"{card.Id}: prints {string.Join(", ", g.Select(p => p.Id))} are the same print"));

    /// <summary>
    /// D58: a booster set prints each rarity in a fixed pattern of regular finishes (Prismatic Evolutions rares: holo +
    /// reverse). A card whose regular finishes differ from the pattern of at least <paramref name="share"/> of its rarity
    /// (groups of at least <paramref name="minCards"/> cards) is probably a source error, e.g. a normal print of an SV rare.
    /// </summary>
    public static IEnumerable<string> PatternOutliers(IEnumerable<CatalogCard> cards, int minCards = 5, double share = 0.8)
    {
        foreach (var rarity in cards.GroupBy(c => c.Rarity))
        {
            var patterns = rarity.Select(c => (Card: c, Pattern: Pattern(c))).ToList();
            if (patterns.Count < minCards) continue;
            var usual = patterns.GroupBy(p => p.Pattern).OrderByDescending(g => g.Count()).First();
            if (usual.Count() < share * patterns.Count) continue;
            foreach (var (card, pattern) in patterns.Where(p => p.Pattern != usual.Key))
                yield return $"{card.Id} ({card.Rarity}): {Show(pattern)}, usual {Show(usual.Key)} ({usual.Count()} of {patterns.Count})";
        }
    }

    /// <summary>The finishes of the regular prints (no tags, no edition), sorted.</summary>
    private static string Pattern(CatalogCard card) =>
        string.Join('+', card.Prints.Where(p => p.Tags is null && p.Edition is null).Select(p => p.Finish).Distinct().Order(StringComparer.Ordinal));

    private static string Show(string pattern) => pattern.Length == 0 ? "no regular print" : pattern;
}
