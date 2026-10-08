using System.Text.Json.Nodes;
using CatalogBuilder.Format;
using CatalogBuilder.Normalization;
using CatalogBuilder.Pipeline;
using CatalogBuilder.Sources;

namespace CatalogBuilder.Tests;

public class CardMapperTests
{
    private static readonly string Root = AppContext.BaseDirectory;

    [Fact]
    public void German_card_takes_enumerations_from_reference_and_texts_from_own_record()
    {
        var vocabulary = Vocabulary();
        var card = new CardMapper(vocabulary, "2026-10-05").Map("de", "sv03", GermanCharizard(), EnglishCharizard());

        Assert.Equal("de/sv03/125", card.Id);
        Assert.Equal("Glurak-ex", card.Name);
        Assert.Equal("double-rare", card.Rarity);
        Assert.Equal(["stage2", "ex"], card.Subtypes);
        Assert.Equal(["darkness"], card.Types);
        Assert.Equal("ability", card.Abilities![0].Kind);
        Assert.Equal("Infernalische Herrschaft", card.Abilities[0].Name);
        Assert.Equal(["fire", "fire"], card.Attacks![0].Cost);
        Assert.Equal("Brennende Finsternis", card.Attacks[0].Name);
        Assert.Equal(["de/sv03/125/holo", "de/sv03/125/holo~jumbo"], card.Prints.Select(p => p.Id));
        Assert.Empty(vocabulary.Problems);
    }

    [Theory]
    [InlineData("G", "G")]
    [InlineData("j", "J")]
    [InlineData(" h ", "H")]
    [InlineData("None", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Regulation_marks_are_normalized(string? raw, string? expected)
    {
        var vocabulary = Vocabulary();
        var card = new CardMapper(vocabulary, "2026-10-05").Map("en", "sv03", WithMark(EnglishCharizard(), raw), null);

        Assert.Equal(expected, card.RegulationMark);
        Assert.Empty(vocabulary.Problems);
    }

    [Fact]
    public void Invalid_regulation_marks_are_dropped_and_reported_and_the_reference_mark_is_used()
    {
        var vocabulary = Vocabulary();
        var mapper = new CardMapper(vocabulary, "2026-10-05");

        Assert.Null(mapper.Map("en", "sv03", WithMark(EnglishCharizard(), "GH"), null).RegulationMark);
        Assert.Equal("G", mapper.Map("de", "sv03", WithMark(GermanCharizard(), "None"), EnglishCharizard()).RegulationMark);
        Assert.Equal(["Invalid regulationMark 'GH' (expected a single letter) – 1×, e.g. en/sv03-125"], vocabulary.Problems);
    }

    [Fact]
    public void Mapped_set_file_is_valid_against_schema()
    {
        var card = new CardMapper(Vocabulary(), "2026-10-05").Map("de", "sv03", GermanCharizard(), EnglishCharizard());
        var file = new SetFile(CatalogJson.FormatVersion, new CatalogSet
        {
            Id = "de/sv03", Code = "sv03", Language = "de", Name = "Obsidian Flammen",
            Provenance = new Provenance("tcgdex", "2026-10-05", "high"),
        }, [card]);

        var errors = new SchemaValidator(Path.Combine(Root, "schema", "v1")).Validate("set", CatalogJson.Serialize(file));

        Assert.Empty(errors);
    }

    [Fact]
    public void Unknown_value_is_reported_as_problem()
    {
        var vocabulary = Vocabulary();
        var english = EnglishCharizard();
        english.Rarity = "Mythical Sparkle Rare";

        var card = new CardMapper(vocabulary, "2026-10-05").Map("en", "sv03", english, null);

        Assert.Equal("none", card.Rarity);
        Assert.Contains(vocabulary.Problems, p => p.Contains("Mythical Sparkle Rare", StringComparison.Ordinal));
    }

    private static TcgdexCard WithMark(TcgdexCard card, string? mark)
    {
        card.RegulationMark = mark;
        return card;
    }

    private static Vocabulary Vocabulary()
    {
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "config", "pipeline.json")))!;
        var valueMap = config["valueMap"]!.AsObject().ToDictionary(
            p => p.Key,
            p => (IReadOnlyDictionary<string, string>)p.Value!.AsObject().ToDictionary(e => e.Key, e => (string)e.Value!));
        return new Vocabulary(JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "config", "vocabulary.json")))!, valueMap);
    }

    private static TcgdexCard EnglishCharizard() => new()
    {
        Id = "sv03-125", LocalId = "125", Name = "Charizard ex", Category = "Pokemon", Rarity = "Double rare",
        Hp = 330, Types = ["Darkness"], DexId = [6], Stage = "Stage2", Suffix = "ex", RegulationMark = "G", Retreat = 2,
        Image = "https://assets.tcgdex.net/en/sv/sv03/125",
        Abilities = [new() { Type = "Ability", Name = "Infernal Reign", Effect = "…" }],
        Attacks = [new() { Name = "Burning Darkness", Cost = ["Fire", "Fire"], Damage = "180+", Effect = "…" }],
        Weaknesses = [new() { Type = "Grass", Value = "×2" }],
        VariantsDetailed = [new() { Type = "holo", Size = "standard" }, new() { Type = "holo", Size = "jumbo" }],
        Set = new() { Id = "sv03" },
    };

    private static TcgdexCard GermanCharizard() => new()
    {
        Id = "sv03-125", LocalId = "125", Name = "Glurak-ex", Category = "Pokémon", Rarity = "Doppelselten",
        Hp = 330, Types = ["Unlicht"], DexId = [6], Stage = "Rang 2", Suffix = "ex", RegulationMark = "G", Retreat = 2,
        Image = "https://assets.tcgdex.net/de/sv/sv03/125",
        Abilities = [new() { Type = "Fähigkeit", Name = "Infernalische Herrschaft", Effect = "…" }],
        Attacks = [new() { Name = "Brennende Finsternis", Cost = ["Feuer", "Feuer"], Damage = "180+", Effect = "…" }],
        Weaknesses = [new() { Type = "Pflanze", Value = "×2" }],
        VariantsDetailed = [new() { Type = "Holo", Size = "Standard" }, new() { Type = "Holo", Size = "Jumbo" }],
        Set = new() { Id = "sv03" },
    };
}
