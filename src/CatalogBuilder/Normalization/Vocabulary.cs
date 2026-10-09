using System.Text.Json.Nodes;

namespace CatalogBuilder.Normalization;

/// <summary>
/// Maps source values to vocabulary keys (C5: display names come from our vocabulary, not from the source).
/// Unknown values are collected as problems so the maintainer can extend the vocabulary / value map.
/// </summary>
public sealed class Vocabulary
{
    private readonly Dictionary<string, HashSet<string>> _sections;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _valueMap;
    private readonly SortedDictionary<string, (int Count, string Example)> _problems = new(StringComparer.Ordinal);

    public Vocabulary(JsonNode vocabulary, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> valueMap)
    {
        _valueMap = valueMap;
        _sections = vocabulary.AsObject()
            .Where(p => p.Value is JsonArray array && array.All(e => e is JsonObject))
            .ToDictionary(
                p => p.Key,
                p => p.Value!.AsArray().Select(e => (string)e!["key"]!).ToHashSet(StringComparer.Ordinal));
    }

    public IReadOnlyList<string> Problems =>
        [.. _problems.Select(p => $"{p.Key} – {p.Value.Count}×, e.g. {p.Value.Example}")];

    /// <summary>Maps <paramref name="raw"/> (field <paramref name="field"/>) to a key that must exist in <paramref name="section"/>.</summary>
    public string? Map(string field, string section, string? raw, string context)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var key = _valueMap.TryGetValue(field, out var map) && map.TryGetValue(raw, out var mapped) ? mapped : Keys.Slug(raw);
        if (_sections.TryGetValue(section, out var keys) && keys.Contains(key)) return key;

        var problem = $"Unknown {field} '{raw}' (key '{key}', vocabulary section '{section}')";
        _problems[problem] = _problems.TryGetValue(problem, out var known) ? (known.Count + 1, known.Example) : (1, context);
        return null;
    }

    /// <summary>
    /// Regulation marks are single upper-case letters (#33): the source also delivers lower case ("j") and the
    /// string "None". Lower case is upper-cased, "None"/empty means no mark, anything else is a problem.
    /// </summary>
    public string? RegulationMark(string? raw, string context)
    {
        var mark = raw?.Trim();
        if (string.IsNullOrEmpty(mark) || mark.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        mark = mark.ToUpperInvariant();
        if (mark is [>= 'A' and <= 'Z']) return mark;

        var problem = $"Invalid regulationMark '{raw}' (expected a single letter)";
        _problems[problem] = _problems.TryGetValue(problem, out var known) ? (known.Count + 1, known.Example) : (1, context);
        return null;
    }

    /// <summary>
    /// Translates localized stamp and foil values of a print key (value map fields <c>stamp</c> and <c>foil</c>, keyed by
    /// slug) – variant tags must be the same in every language (catalog issue #1).
    /// </summary>
    public PrintKey NormalizePrint(PrintKey key) => key.Normalize(s => Translate("stamp", s), f => Translate("foil", f));

    private string Translate(string field, string slug) =>
        _valueMap.TryGetValue(field, out var map) && map.TryGetValue(slug, out var mapped) ? mapped : slug;

    public IReadOnlyList<string>? MapAll(string field, string section, IEnumerable<string>? raws, string context)
    {
        var keys = (raws ?? []).Select(r => Map(field, section, r, context)).OfType<string>().ToList();
        return keys.Count > 0 ? keys : null;
    }
}
