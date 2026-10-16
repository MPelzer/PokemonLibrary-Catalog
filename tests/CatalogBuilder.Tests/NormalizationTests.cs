using System.Text.Json.Nodes;
using CatalogBuilder.Format;
using CatalogBuilder.Normalization;
using CatalogBuilder.Pipeline;

namespace CatalogBuilder.Tests;

public class NormalizationTests
{
    [Theory]
    [InlineData("Double rare", "double-rare")]
    [InlineData("Poké-POWER", "poke-power")]
    [InlineData("Fähigkeit", "fahigkeit")]
    [InlineData("  TAG TEAM-GX ", "tag-team-gx")]
    public void Slug_produces_vocabulary_keys(string raw, string expected) => Assert.Equal(expected, Keys.Slug(raw));

    [Theory]
    [InlineData("sv03", "sv03")]
    [InlineData("sv03.5", "sv03-5")]
    [InlineData("swsh12.5gg", "swsh12-5gg")]
    [InlineData("SV3", "sv3")]
    public void SetCode_is_lower_case_without_dots(string tcgdexId, string expected) => Assert.Equal(expected, Ids.SetCode(tcgdexId));

    [Theory]
    [InlineData("125", "125")]
    [InlineData("TG01", "TG01")]
    [InlineData("!", "%21")]
    [InlineData("%3F", "%3F")] // already encoded in the source: decoded first, then encoded once
    public void Number_is_percent_encoded_once(string printed, string expected) => Assert.Equal(expected, Ids.Number(printed));

    [Fact]
    public void Print_id_contains_finish_edition_and_sorted_tags()
    {
        var key = PrintKey.FromVariant("holo", "jumbo", ["1st-edition", "staff"], "cosmos");

        Assert.Equal("en/base1/4/holo.first-edition~foil-cosmos~jumbo~stamp-staff", Ids.Print("en/base1/4", key));
    }

    [Fact]
    public void Localized_variant_values_give_the_same_key()
    {
        Assert.Equal(PrintKey.FromVariant("holo", "standard", null, null), PrintKey.FromVariant("Holo", "Standard", null, null));
    }

    [Fact]
    public void Identical_variants_are_merged_into_one_print()
    {
        var prints = Pipeline.CardMapper.MapPrints("en/sv03/125",
        [
            new() { Type = "holo", Size = "standard" },
            new() { Type = "holo", Size = "standard", Stamp = ["player-rewards-program"] },
            new() { Type = "holo", Size = "standard", Stamp = ["player-rewards-program"] },
        ]);

        Assert.Equal(["en/sv03/125/holo", "en/sv03/125/holo~stamp-player-rewards-program"], prints.Select(p => p.Id));
    }

    [Fact]
    public void Localized_stamps_and_foils_get_language_independent_tags_but_keep_their_print_id()
    {
        var translations = new Dictionary<string, string> { ["mitarbeiter"] = "staff", ["kosmos"] = "cosmos", ["1-auflage"] = "1st-edition" };
        string Translate(string value) => translations.GetValueOrDefault(value, value);

        var prints = Pipeline.CardMapper.MapPrints("de/base1/4",
            [new() { Type = "holo", Size = "standard", Stamp = ["1. Auflage", "Mitarbeiter"], Foil = "Kosmos" }],
            key => key.Normalize(Translate, Translate));

        var print = Assert.Single(prints);
        Assert.Equal("de/base1/4/holo~foil-kosmos~stamp-1-auflage~stamp-mitarbeiter", print.Id); // unchanged (FR-CAT-10)
        Assert.Equal("first-edition", print.Edition);
        Assert.Equal(["foil-cosmos", "stamp-staff"], print.Tags);
    }

    [Fact]
    public void Untranslated_values_stay_as_they_are()
    {
        var key = PrintKey.FromVariant("holo", "jumbo", ["staff"], "cosmos");

        Assert.Equal(key, key.Normalize(s => s, f => f));
    }

    private static Vocabulary VocabularyWith(Dictionary<string, Dictionary<string, string>> valueMap) => new(JsonNode.Parse("""
        {
          "finishes": [ { "key": "normal" }, { "key": "reverse" }, { "key": "metal" } ],
          "variantTags": [ { "key": "foil-pokeball", "kind": "reverse-pattern" }, { "key": "stamp-league", "kind": "stamp" } ]
        }
        """)!, valueMap.ToDictionary(m => m.Key, m => (IReadOnlyDictionary<string, string>)m.Value));

    [Fact]
    public void A_foil_mapped_to_a_stamp_becomes_a_stamp_and_the_finish_is_translated_but_the_id_stays()
    {
        var vocabulary = VocabularyWith(new()
        {
            ["foil"] = new() { ["league"] = "stamp-league" },
            ["finish"] = new() { ["metall"] = "metal" },
        });

        var prints = CardMapper.MapPrints("en/pl3/60", [
            new() { Type = "reverse", Foil = "League" },
            new() { Type = "metall" },
        ], key => vocabulary.NormalizePrint(key, "test"));

        Assert.Equal("en/pl3/60/reverse~foil-league", prints[0].Id); // unchanged (FR-CAT-10)
        Assert.Equal(["stamp-league"], prints[0].Tags);
        Assert.Equal("en/pl3/60/metall", prints[1].Id);
        Assert.Equal("metal", prints[1].Finish);
        Assert.Empty(vocabulary.Problems);
    }

    [Fact]
    public void Finishes_and_tags_outside_the_vocabulary_are_problems()
    {
        var vocabulary = VocabularyWith([]);

        CardMapper.MapPrints("en/x/1", [new() { Type = "metall", Foil = "Glitter" }], key => vocabulary.NormalizePrint(key, "en/x-1"));

        Assert.Equal([
            "Unknown finish 'metall' (vocabulary section 'finishes') – 1×, e.g. en/x-1",
            "Unknown tag 'foil-glitter' (vocabulary section 'variantTags') – 1×, e.g. en/x-1",
        ], vocabulary.Problems);
    }

    private static CatalogCard Card(string number, string rarity, params (string Finish, string[]? Tags)[] prints) => new()
    {
        Id = "en/sv06/" + number, Number = number, Name = "Card " + number, Supertype = "pokemon", Rarity = rarity,
        Prints = [.. prints.Select(p => new CatalogPrint { Id = $"en/sv06/{number}/{p.Finish}{string.Concat((p.Tags ?? []).Select(t => "~" + t))}", Finish = p.Finish, Tags = p.Tags })],
        Provenance = new Provenance("tcgdex", "2026-10-16", "high"),
    };

    [Fact]
    public void A_card_that_breaks_the_print_pattern_of_its_rarity_is_reported()
    {
        CatalogCard[] cards =
        [
            .. Enumerable.Range(1, 5).Select(i => Card($"00{i}", "rare", ("holo", null), ("reverse", null), ("reverse", ["foil-pokeball"]))),
            Card("006", "rare", ("normal", null), ("holo", null), ("reverse", null)),
            Card("100", "double-rare", ("holo", null)), // too few cards for a pattern
            Card("101", "double-rare", ("normal", null)),
        ];

        Assert.Equal(["en/sv06/006 (rare): holo+normal+reverse, usual holo+reverse (5 of 6)"], PrintChecks.PatternOutliers(cards));
    }

    [Fact]
    public void Prints_that_became_equal_are_reported()
    {
        var card = Card("001", "common", ("reverse", ["stamp-league"]), ("reverse", null));
        var duplicate = card with { Prints = [.. card.Prints, new CatalogPrint { Id = "en/sv06/001/reverse~foil-league", Finish = "reverse", Tags = ["stamp-league"] }] };

        Assert.Empty(PrintChecks.Duplicates([card]));
        Assert.Equal(["en/sv06/001: prints en/sv06/001/reverse~stamp-league, en/sv06/001/reverse~foil-league are the same print"], PrintChecks.Duplicates([duplicate]));
    }
}
