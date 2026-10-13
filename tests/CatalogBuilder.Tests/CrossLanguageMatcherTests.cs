using CatalogBuilder.Pipeline;
using CatalogBuilder.Sources;

namespace CatalogBuilder.Tests;

/// <summary>D53: Japanese cards are mapped to their international counterpart by card data.</summary>
public class CrossLanguageMatcherTests
{
    private static readonly Dictionary<string, string?> JaReleases = new() { ["SV3"] = "2023-07-28", ["SV2a"] = "2023-06-16" };
    private static readonly Dictionary<string, string?> EnReleases = new() { ["sv03"] = "2023-08-11", ["svp"] = "2023-03-31", ["sv03.5"] = "2023-09-22" };

    [Fact]
    public void A_unique_pokemon_is_matched_with_high_confidence()
    {
        var ja = Pokemon("SV3-066", "SV3", "066", 6, "5ban Graphics");
        var en = Pokemon("sv03-125", "sv03", "125", 6, "5ban Graphics");

        var match = Assert.Single(CrossLanguageMatcher.Match([ja], [en, Pokemon("sv03-001", "sv03", "001", 1, "5ban Graphics")], JaReleases, EnReleases));

        Assert.Equal(("SV3-066", new CounterpartMatch("sv03-125", "high")), (match.Key, match.Value));
    }

    [Fact]
    public void Versions_of_the_same_card_are_paired_in_number_order_within_the_counterpart_set()
    {
        // The Japanese set has the regular card and one full art, the English set has three versions and a promo.
        List<TcgdexCard> ja = [Pokemon("SV3-066", "SV3", "066", 6, "5ban Graphics"), Pokemon("SV3-115", "SV3", "115", 6, "5ban Graphics"),
            Pokemon("SV3-001", "SV3", "001", 1, "Other")];
        List<TcgdexCard> en = [Pokemon("sv03-228", "sv03", "228", 6, "5ban Graphics"), Pokemon("sv03-125", "sv03", "125", 6, "5ban Graphics"),
            Pokemon("sv03-215", "sv03", "215", 6, "5ban Graphics"), Pokemon("svp-074", "svp", "074", 6, "5ban Graphics"),
            Pokemon("sv03-001", "sv03", "001", 1, "Other")];

        var matches = CrossLanguageMatcher.Match(ja, en, JaReleases, EnReleases);

        Assert.Equal(new CounterpartMatch("sv03-125", "low"), matches["SV3-066"]);
        Assert.Equal(new CounterpartMatch("sv03-215", "low"), matches["SV3-115"]);
        Assert.Equal("high", matches["SV3-001"].Confidence); // the vote for sv03 as counterpart set
    }

    [Fact]
    public void Trainers_are_matched_only_one_to_one_within_the_counterpart_set()
    {
        List<TcgdexCard> ja = [Pokemon("SV3-001", "SV3", "001", 1, "A"), Trainer("SV3-090", "SV3", "090", "Supporter", "Ryota Murayama"),
            Trainer("SV3-091", "SV3", "091", "Item", "5ban Graphics"), Trainer("SV3-092", "SV3", "092", "Item", "5ban Graphics")];
        List<TcgdexCard> en = [Pokemon("sv03-001", "sv03", "001", 1, "A"), Trainer("sv03-190", "sv03", "190", "Supporter", "Ryota Murayama"),
            Trainer("sv03-191", "sv03", "191", "Item", "5ban Graphics"), Trainer("sv03-192", "sv03", "192", "Item", "5ban Graphics"),
            Trainer("svp-010", "svp", "010", "Supporter", "Ryota Murayama")];

        var matches = CrossLanguageMatcher.Match(ja, en, JaReleases, EnReleases);

        Assert.Equal(new CounterpartMatch("sv03-190", "medium"), matches["SV3-090"]);
        Assert.False(matches.ContainsKey("SV3-091")); // two items by the same illustrator: ambiguous
        Assert.False(matches.ContainsKey("SV3-092"));
    }

    [Fact]
    public void Trainers_of_a_set_without_counterpart_stay_unmatched() =>
        Assert.Empty(CrossLanguageMatcher.Match([Trainer("SV2a-150", "SV2a", "150", "Supporter", "Ryota Murayama")],
            [Trainer("sv03-190", "sv03", "190", "Supporter", "Ryota Murayama")], JaReleases, EnReleases));

    [Fact]
    public void A_missing_species_is_taken_from_the_longest_species_name_in_the_card_name()
    {
        var charizard = Pokemon("SV3-066", "SV3", "066", null, "5ban Graphics", "リザードンex");
        var mewtwo = Pokemon("SV2a-150", "SV2a", "150", null, "x", "ミュウツーex");
        var trainer = Trainer("SV3-090", "SV3", "090", "Supporter", "x");

        var filled = CrossLanguageMatcher.FillSpecies([charizard, mewtwo, trainer], new Dictionary<string, int>
        {
            ["リザード"] = 5, ["リザードン"] = 6, ["ミュウ"] = 151, ["ミュウツー"] = 150,
        });

        Assert.Equal(2, filled);
        Assert.Equal([6], charizard.DexId);
        Assert.Equal([150], mewtwo.DexId);
        Assert.Null(trainer.DexId);
    }

    [Theory]
    [InlineData("5ban Graphics", "5bangraphics")]
    [InlineData("AKIRA  EGAWA", "akiraegawa")]
    [InlineData("Ｍｉｔｓｕｈｉｒｏ　Ａｒｉｔａ", "mitsuhiroarita")] // full-width in Japanese records
    [InlineData(" ", null)]
    public void Illustrators_are_compared_without_spacing_and_case(string raw, string? expected) =>
        Assert.Equal(expected, CrossLanguageMatcher.Illustrator(raw));

    [Theory]
    [InlineData("30×", "30x")]
    [InlineData("180+", "180+")]
    [InlineData(null, "")]
    public void Damage_is_normalized(string? raw, string expected) => Assert.Equal(expected, CrossLanguageMatcher.Damage(raw));

    private static TcgdexCard Pokemon(string id, string set, string number, int? dex, string illustrator, string name = "x") => new()
    {
        Id = id, LocalId = number, Name = name, Category = "Pokemon", Hp = 330, DexId = dex is null ? null : [dex],
        Illustrator = illustrator, Types = ["Darkness"], Attacks = [new TcgdexAttack { Damage = "180+" }], Set = new TcgdexSetRef { Id = set },
    };

    private static TcgdexCard Trainer(string id, string set, string number, string type, string illustrator) => new()
    {
        Id = id, LocalId = number, Name = "x", Category = "Trainer", TrainerType = type, Illustrator = illustrator, Set = new TcgdexSetRef { Id = set },
    };
}
