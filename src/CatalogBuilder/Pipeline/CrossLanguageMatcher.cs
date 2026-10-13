using System.Globalization;
using System.Text;
using CatalogBuilder.Sources;

namespace CatalogBuilder.Pipeline;

/// <summary>A card of a language with its own numbering (Japanese) mapped to its international counterpart.</summary>
public sealed record CounterpartMatch(string RefCardId, string Confidence);

/// <summary>
/// D53: maps cards of a language with its own sets and numbering (Japanese) to the reference card with the same
/// printed content. TCGdex has no link between them, so the card data is compared:
/// <list type="number">
/// <item>Pokémon by species, HP, illustrator, attack damage and types – a unique reference card is a match
/// (confidence high) and a vote for the counterpart set of the card's set;</item>
/// <item>the counterpart sets of a set are the reference sets with at least a quarter of the top vote count;</item>
/// <item>ambiguous Pokémon (the same card in several versions) are paired by number order when the counterpart set has
/// at least as many versions (confidence low if it has more), otherwise resolved within the counterpart sets, then by the earliest reference release not
/// before the card's own release (confidence medium);</item>
/// <item>trainers and energy (no species, HP or damage to compare) by illustrator and subtype within the counterpart
/// sets only, if the combination is unique on both sides (confidence medium) – items share illustrators.</item>
/// </list>
/// Everything else stays unmatched – a wrong group is worse than none.
/// </summary>
public static class CrossLanguageMatcher
{
    /// <param name="cards">The cards of the language to map.</param>
    /// <param name="refCards">The reference-language cards.</param>
    /// <param name="releases">Release date (ISO) per set id of <paramref name="cards"/>; missing = unknown.</param>
    /// <param name="refReleases">Release date per set id of <paramref name="refCards"/>.</param>
    /// <returns>Match per source card id.</returns>
    public static IReadOnlyDictionary<string, CounterpartMatch> Match(IReadOnlyList<TcgdexCard> cards, IReadOnlyList<TcgdexCard> refCards,
        IReadOnlyDictionary<string, string?> releases, IReadOnlyDictionary<string, string?> refReleases)
    {
        var pokemonIndex = refCards.Select(c => (Key: PokemonKey(c), Card: c)).Where(x => x.Key is not null)
            .ToLookup(x => x.Key!, x => x.Card, StringComparer.Ordinal);
        var otherIndex = refCards.Select(c => (Key: OtherKey(c), Card: c)).Where(x => x.Key is not null)
            .ToLookup(x => x.Key!, x => x.Card, StringComparer.Ordinal);

        var result = new Dictionary<string, CounterpartMatch>(StringComparer.Ordinal);
        var votes = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal); // set → reference set → count
        var pending = new List<(TcgdexCard Card, List<TcgdexCard> Candidates)>();

        // 1. Unique Pokémon matches.
        foreach (var card in cards)
        {
            if (PokemonKey(card) is not { } key) continue;
            var candidates = pokemonIndex[key].ToList();
            if (candidates.Count == 1)
            {
                result[card.Id] = new CounterpartMatch(candidates[0].Id, "high");
                Vote(votes, card.Set!.Id, candidates[0].Set!.Id);
            }
            else if (candidates.Count > 1) pending.Add((card, candidates));
        }

        var counterparts = votes.ToDictionary(v => v.Key, v =>
        {
            var top = v.Value.Values.Max();
            return v.Value.Where(s => s.Value * 4 >= top).Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        }, StringComparer.Ordinal);

        // 2. Ambiguous Pokémon. The same card often appears several times in a set (regular, full art, gold) on both
        //    sides: equally many versions in the counterpart set are paired in the order of their numbers. Otherwise the
        //    counterpart sets narrow the candidates, then the release order decides.
        foreach (var group in pending.GroupBy(p => (p.Card.Set!.Id, PokemonKey(p.Card)!)))
        {
            var own = group.OrderBy(p => p.Card.LocalId, NaturalComparer.Instance).ToList();
            var inSets = counterparts.TryGetValue(group.Key.Item1, out var sets)
                ? own[0].Candidates.Where(c => sets.Contains(c.Set!.Id)).ToList() : [];
            if (inSets.Count >= own.Count && inSets.Select(c => c.Set!.Id).Distinct().Count() == 1)
            {
                // Fewer own versions (the source lacks some): the lowest numbers are the regular cards on both sides.
                var ordered = inSets.OrderBy(c => c.LocalId, NaturalComparer.Instance).ToList();
                var confidence = inSets.Count == own.Count ? "medium" : "low";
                for (var i = 0; i < own.Count; i++) result[own[i].Card.Id] = new CounterpartMatch(ordered[i].Id, confidence);
                continue;
            }
            foreach (var (card, candidates) in own)
                if (Resolve(card, candidates, counterparts, releases, refReleases) is { } match) result[card.Id] = new CounterpartMatch(match.Id, "medium");
        }

        // 3. Trainers and energy within the counterpart sets, one-to-one only.
        var ownKeys = cards.Select(c => (Key: OtherKey(c), Card: c)).Where(x => x.Key is not null)
            .GroupBy(x => (x.Card.Set!.Id, x.Key!)).ToDictionary(g => g.Key, g => g.Count());
        foreach (var card in cards)
        {
            if (result.ContainsKey(card.Id) || OtherKey(card) is not { } key || !counterparts.TryGetValue(card.Set!.Id, out var sets)
                || ownKeys[(card.Set.Id, key)] > 1) continue;
            var candidates = otherIndex[key].Where(c => sets.Contains(c.Set!.Id)).ToList();
            if (candidates.Count == 1) result[card.Id] = new CounterpartMatch(candidates[0].Id, "medium");
        }
        return result;
    }

    /// <summary>
    /// Fills a missing species from the card name (many Japanese records have no <c>dexId</c>): the longest species name
    /// of the language contained in the card name ("リザードンex" → リザードン → 6, "ヒビキのピチュー" → ピチュー).
    /// </summary>
    public static int FillSpecies(IEnumerable<TcgdexCard> cards, IReadOnlyDictionary<string, int> speciesByName)
    {
        var names = speciesByName.Keys.Where(n => n.Length >= 2).OrderByDescending(n => n.Length).ToList();
        var filled = 0;
        foreach (var card in cards)
        {
            if (!string.Equals(card.Category, "Pokemon", StringComparison.OrdinalIgnoreCase) || card.DexId?.OfType<int>().Any() == true) continue;
            if (names.FirstOrDefault(n => card.Name.Contains(n, StringComparison.Ordinal)) is not { } name) continue;
            card.DexId = [speciesByName[name]];
            filled++;
        }
        return filled;
    }

    private static void Vote(Dictionary<string, Dictionary<string, int>> votes, string set, string refSet)
    {
        if (!votes.TryGetValue(set, out var bySet)) votes[set] = bySet = new Dictionary<string, int>(StringComparer.Ordinal);
        bySet[refSet] = bySet.GetValueOrDefault(refSet) + 1;
    }

    private static TcgdexCard? Resolve(TcgdexCard card, List<TcgdexCard> candidates, Dictionary<string, HashSet<string>> counterparts,
        IReadOnlyDictionary<string, string?> releases, IReadOnlyDictionary<string, string?> refReleases)
    {
        if (counterparts.TryGetValue(card.Set!.Id, out var sets) && candidates.Where(c => sets.Contains(c.Set!.Id)).ToList() is { Count: > 0 } inSets)
            candidates = inSets;
        if (candidates.Count == 1) return candidates[0];

        // The earliest reference release not before the card's own (international sets follow the Japanese ones);
        // a tie within that release is ambiguous.
        var own = releases.GetValueOrDefault(card.Set.Id);
        var dated = candidates.Select(c => (Card: c, Date: refReleases.GetValueOrDefault(c.Set!.Id))).Where(c => c.Date is not null)
            .Where(c => own is null || string.CompareOrdinal(c.Date, own) >= 0).OrderBy(c => c.Date, StringComparer.Ordinal).ToList();
        return dated.Count > 0 && (dated.Count == 1 || dated[1].Date != dated[0].Date) ? dated[0].Card : null;
    }

    /// <summary>Species, HP, illustrator, attack damage and types – <c>null</c> for non-Pokémon or missing data.</summary>
    internal static string? PokemonKey(TcgdexCard card)
    {
        if (!string.Equals(card.Category, "Pokemon", StringComparison.OrdinalIgnoreCase) || card.Set is null) return null;
        var dex = card.DexId?.OfType<int>().Order().ToList() ?? [];
        if (dex.Count == 0 || card.Hp is null || Illustrator(card.Illustrator) is not { } illustrator) return null;
        var damage = string.Join(",", (card.Attacks ?? []).Select(a => Damage(a?.Damage)));
        var types = string.Join(",", (card.Types ?? []).Order(StringComparer.Ordinal));
        return string.Join("|", string.Join(",", dex), card.Hp.Value.ToString(CultureInfo.InvariantCulture), illustrator, damage, types);
    }

    /// <summary>Trainer and energy cards: category, subtype and illustrator.</summary>
    internal static string? OtherKey(TcgdexCard card)
    {
        if (string.Equals(card.Category, "Pokemon", StringComparison.OrdinalIgnoreCase) || card.Set is null
            || Illustrator(card.Illustrator) is not { } illustrator) return null;
        return string.Join("|", card.Category?.ToLowerInvariant(), card.TrainerType?.ToLowerInvariant(), card.EnergyType?.ToLowerInvariant(), illustrator);
    }

    /// <summary>Letters and digits only, lower case – spacing and punctuation differ between the languages' records.</summary>
    internal static string? Illustrator(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var sb = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormKC)) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.Length > 0 ? sb.ToString() : null;
    }

    /// <summary>"30+", "30×", "30x" → "30+", "30x"; empty for no damage.</summary>
    internal static string Damage(string? damage) =>
        (damage ?? "").Normalize(NormalizationForm.FormKC).Replace('×', 'x').Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
}
