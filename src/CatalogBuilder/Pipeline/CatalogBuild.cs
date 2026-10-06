using System.Collections.Concurrent;
using System.Globalization;
using CatalogBuilder.Format;
using CatalogBuilder.Normalization;
using CatalogBuilder.Sources;

namespace CatalogBuilder.Pipeline;

public sealed record CatalogBuildOptions(bool FetchMarketplaceIds, bool Lenient, IReadOnlyList<string>? Languages);

/// <summary>Fetches all sources and writes the catalog files to <c>catalog/</c>.</summary>
public sealed class CatalogBuild(PipelineConfig config, TcgdexClient tcgdex, PokeApiClient pokeApi, TextWriter log)
{
    private readonly string _today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Returns the process exit code.</summary>
    public async Task<int> RunAsync(CatalogBuildOptions options, CancellationToken ct)
    {
        var settings = config.Settings;
        var languages = options.Languages ?? settings.Languages;
        var refLang = settings.ReferenceLanguage;
        var vocabulary = config.CreateVocabulary();
        var mapper = new CardMapper(vocabulary, _today);

        log.WriteLine($"Reference data ({refLang}) …");
        var refSets = await tcgdex.GetSetsAsync(refLang, ct);
        var excludedSetIds = refSets.Where(s => settings.ExcludedSeries.Contains(s.Serie?.Id ?? "")).Select(s => s.Id).ToHashSet();
        var refCards = (await tcgdex.GetCardsAsync(refLang, ct))
            .Where(c => c.Set is not null && !excludedSetIds.Contains(c.Set.Id)).ToList();
        var refById = refCards.ToDictionary(c => c.Id);
        log.WriteLine($"  {refCards.Count} physical cards, {excludedSetIds.Count} excluded sets ({string.Join(", ", settings.ExcludedSeries)})");

        var marketplaceIds = options.FetchMarketplaceIds ? await FetchMarketplaceIdsAsync(refCards, ct) : null;

        foreach (var lang in languages)
        {
            ct.ThrowIfCancellationRequested();
            log.WriteLine($"Language {lang} …");
            var sets = (lang == refLang ? refSets : await tcgdex.GetSetsAsync(lang, ct))
                .Where(s => !excludedSetIds.Contains(s.Id)).ToList();
            var codes = SetCodes(sets);
            var cards = lang == refLang ? refCards : await tcgdex.GetCardsAsync(lang, ct);
            var cardsBySet = cards.Where(c => c.Set is not null && codes.ContainsKey(c.Set.Id)).GroupBy(c => c.Set!.Id)
                .ToDictionary(g => g.Key, g => g.ToList());
            var abbreviations = await FetchAbbreviationsAsync(lang, sets.Where(s => cardsBySet.ContainsKey(s.Id)), ct);

            var written = 0;
            foreach (var set in sets.Where(s => cardsBySet.ContainsKey(s.Id)))
            {
                var setCode = codes[set.Id];
                var setId = Ids.Set(lang, setCode);
                var mapped = cardsBySet[set.Id]
                    .OrderBy(c => c.LocalId, NaturalComparer.Instance)
                    .Select(c => mapper.Map(lang, setCode, c, lang == refLang ? null : refById.GetValueOrDefault(c.Id)))
                    .ToList();

                mapped = marketplaceIds is null
                    ? KeepPreviousExternalIds(lang, setCode, mapped)
                    : ApplyExternalIds(mapped, marketplaceIds);

                var setFile = new SetFile(CatalogJson.FormatVersion, new CatalogSet
                {
                    Id = setId,
                    Code = setCode,
                    Language = lang,
                    Name = set.Name,
                    Series = set.Serie?.Name,
                    Abbreviation = abbreviations.GetValueOrDefault(set.Id),
                    ReleaseDate = set.ReleaseDate,
                    CardCount = set.CardCount is null ? null : new CardCount(set.CardCount.Official, set.CardCount.Total),
                    Images = set.Logo is null && set.Symbol is null ? null : new Images
                    {
                        Logo = set.Logo is null ? null : set.Logo + ".webp",
                        Symbol = set.Symbol is null ? null : set.Symbol + ".webp",
                    },
                    ExternalIds = new Dictionary<string, string> { ["tcgdex"] = set.Id },
                    Provenance = new Provenance("tcgdex", _today, "high"),
                }, mapped);
                Write($"sets/{lang}/{setCode}.json", setFile);

                written++;
            }
            log.WriteLine($"  {written} sets, {cardsBySet.Values.Sum(c => c.Count)} cards");
        }

        log.WriteLine("Species (PokéAPI) …");
        var species = await pokeApi.GetSpeciesAsync(languages, ct);
        Write("species.json", new SpeciesFile(CatalogJson.FormatVersion, [.. species.Select(s => new Species
        {
            Dex = s.Dex,
            Generation = s.Generation,
            Names = s.Names,
            Types = s.Types.Count > 0 ? s.Types : null,
            EvolutionChain = s.EvolutionChain.Count > 0 ? s.EvolutionChain : null,
            FlavorText = s.FlavorText.Count > 0 ? s.FlavorText : null,
            Provenance = new Provenance("pokeapi", _today, "high"),
        })]));
        WriteRaw("vocabulary.json", config.VocabularyJson);

        return Report(vocabulary, options.Lenient);
    }

    private int Report(Vocabulary vocabulary, bool lenient)
    {
        var schemaErrors = new SchemaValidator(config.SchemaDir).ValidateDirectory(config.CatalogDir);
        foreach (var error in schemaErrors.Take(50)) log.WriteLine("  schema: " + error);
        foreach (var problem in vocabulary.Problems) log.WriteLine("  vocabulary: " + problem);

        if (schemaErrors.Count > 0)
        {
            log.WriteLine($"FAILED: {schemaErrors.Count} schema violation(s).");
            return 2;
        }
        if (vocabulary.Problems.Count > 0 && !lenient)
        {
            log.WriteLine($"FAILED: {vocabulary.Problems.Count} unknown vocabulary value(s) – extend config/vocabulary.json or config/pipeline.json (valueMap), or run with --lenient.");
            return 3;
        }
        log.WriteLine("Build OK – catalog/ is valid against schema v1.");
        return 0;
    }

    // ---------- Marketplace ids ----------

    private sealed record CardMarketplaceIds(IReadOnlyDictionary<PrintKey, IReadOnlyDictionary<string, string>> ByPrint);

    /// <summary>
    /// One REST request per reference card (marketplace ids are not language-specific), throttled.
    /// Prices in the same response are ignored – the catalog does not redistribute them (D27).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, CardMarketplaceIds>> FetchMarketplaceIdsAsync(List<TcgdexCard> cards, CancellationToken ct)
    {
        log.WriteLine($"Marketplace ids for {cards.Count} cards (concurrency {config.Settings.RequestConcurrency}) …");
        var result = new ConcurrentDictionary<string, CardMarketplaceIds>();
        var done = 0;
        var failed = 0;
        await Parallel.ForEachAsync(cards, new ParallelOptions { MaxDegreeOfParallelism = config.Settings.RequestConcurrency, CancellationToken = ct },
            async (card, token) =>
            {
                try
                {
                    var full = await tcgdex.GetCardAsync(config.Settings.ReferenceLanguage, card.Id, token);
                    result[card.Id] = new CardMarketplaceIds(MarketplaceIdMapper.Map(full));
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref failed);
                }
                var n = Interlocked.Increment(ref done);
                if (n % 1000 == 0) log.WriteLine($"  {n}/{cards.Count}");
            });
        log.WriteLine($"  done: {result.Count} cards, {failed} failed requests");
        return result;
    }

    private static List<CatalogCard> ApplyExternalIds(List<CatalogCard> cards, IReadOnlyDictionary<string, CardMarketplaceIds> marketplaceIds) =>
        [.. cards.Select(card =>
        {
            if (!marketplaceIds.TryGetValue(card.ExternalIds!["tcgdex"], out var cardIds)) return card;
            var byPrintId = cardIds.ByPrint.ToDictionary(p => Ids.Print(card.Id, p.Key), p => p.Value);
            return card with { Prints = [.. card.Prints.Select(p => byPrintId.TryGetValue(p.Id, out var ids) ? p with { ExternalIds = ids } : p)] };
        })];

    private List<CatalogCard> KeepPreviousExternalIds(string lang, string setCode, List<CatalogCard> cards)
    {
        var previous = Read<SetFile>($"sets/{lang}/{setCode}.json");
        if (previous is null) return cards;
        var ids = previous.Cards.SelectMany(c => c.Prints).Where(p => p.ExternalIds is not null).ToDictionary(p => p.Id, p => p.ExternalIds);
        return [.. cards.Select(card => card with
        {
            Prints = [.. card.Prints.Select(p => ids.TryGetValue(p.Id, out var e) ? p with { ExternalIds = e } : p)],
        })];
    }

    // ---------- Sets ----------

    private static Dictionary<string, string> SetCodes(IEnumerable<TcgdexSet> sets)
    {
        var codes = sets.ToDictionary(s => s.Id, s => Ids.SetCode(s.Id));
        var collisions = codes.GroupBy(c => c.Value).Where(g => g.Count() > 1).Select(g => $"{g.Key}: {string.Join(", ", g.Select(c => c.Key))}").ToList();
        return collisions.Count == 0 ? codes : throw new InvalidOperationException("Set code collisions: " + string.Join("; ", collisions));
    }

    private async Task<IReadOnlyDictionary<string, string>> FetchAbbreviationsAsync(string lang, IEnumerable<TcgdexSet> sets, CancellationToken ct)
    {
        var result = new ConcurrentDictionary<string, string>();
        await Parallel.ForEachAsync(sets, new ParallelOptions { MaxDegreeOfParallelism = config.Settings.RequestConcurrency, CancellationToken = ct },
            async (set, token) =>
            {
                try
                {
                    if ((await tcgdex.GetSetAbbreviationAsync(lang, set.Id, token))?.Official is { Length: > 0 } abbreviation)
                        result[set.Id] = abbreviation;
                }
                catch (HttpRequestException ex)
                {
                    log.WriteLine($"  warning: no set details for {lang}/{set.Id}: {ex.Message}");
                }
            });
        return result;
    }

    // ---------- Files ----------

    private void Write<T>(string relativePath, T value) => WriteRaw(relativePath, CatalogJson.Serialize(value));

    private void WriteRaw(string relativePath, string content)
    {
        var path = Path.Combine(config.CatalogDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.ReplaceLineEndings("\n"));
    }

    private T? Read<T>(string relativePath)
    {
        var path = Path.Combine(config.CatalogDir, relativePath);
        return File.Exists(path) ? CatalogJson.Deserialize<T>(File.ReadAllText(path)) : default;
    }
}

/// <summary>Orders card numbers like "2" &lt; "10" &lt; "TG01"; non-numeric parts compare ordinally.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        var (xn, xr) = Split(x ?? "");
        var (yn, yr) = Split(y ?? "");
        if (xn is not null && yn is not null && xn != yn) return xn.Value.CompareTo(yn.Value);
        if (xn is null != yn is null) return xn is null ? 1 : -1;
        return string.CompareOrdinal(xr, yr);
    }

    private static (long? Number, string Remainder) Split(string s)
    {
        var digits = s.TakeWhile(char.IsAsciiDigit).Count();
        return digits == 0 || digits > 18 ? (null, s) : (long.Parse(s[..digits], CultureInfo.InvariantCulture), s[digits..]);
    }
}
