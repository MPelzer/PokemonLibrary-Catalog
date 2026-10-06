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
}
