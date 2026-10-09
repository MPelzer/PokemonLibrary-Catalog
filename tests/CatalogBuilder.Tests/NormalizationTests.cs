using CatalogBuilder.Normalization;

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
}
