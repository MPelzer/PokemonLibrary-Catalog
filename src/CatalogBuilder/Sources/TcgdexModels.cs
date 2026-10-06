using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CatalogBuilder.Sources;

// Subset of the TCGdex API v2 card/set model used by the pipeline (GraphQL and REST share field names).

public sealed class TcgdexCard
{
    public string Id { get; set; } = "";
    public string LocalId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? Rarity { get; set; }
    public string? Illustrator { get; set; }
    public int? Hp { get; set; }
    public List<string>? Types { get; set; }
    public List<int>? DexId { get; set; }
    public string? Stage { get; set; }
    public string? Suffix { get; set; }
    public string? EvolveFrom { get; set; }
    public string? RegulationMark { get; set; }
    public int? Retreat { get; set; }
    public string? Image { get; set; }
    public string? TrainerType { get; set; }
    public string? EnergyType { get; set; }
    public List<TcgdexAttack?>? Attacks { get; set; }
    public List<TcgdexAbility?>? Abilities { get; set; }
    public List<TcgdexTypeModifier?>? Weaknesses { get; set; }
    public List<TcgdexTypeModifier?>? Resistances { get; set; }
    [JsonPropertyName("variants_detailed")] public List<TcgdexVariant>? VariantsDetailed { get; set; }
    public TcgdexSetRef? Set { get; set; }
}

public sealed class TcgdexSetRef
{
    public string Id { get; set; } = "";
}

public sealed class TcgdexAttack
{
    public string? Name { get; set; }
    public List<string>? Cost { get; set; }
    [JsonConverter(typeof(StringOrNumberConverter))] public string? Damage { get; set; }
    public string? Effect { get; set; }
}

public sealed class TcgdexAbility
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Effect { get; set; }
}

public sealed class TcgdexTypeModifier
{
    public string? Type { get; set; }
    public string? Value { get; set; }
}

public sealed class TcgdexVariant
{
    public string Type { get; set; } = "";
    public string? Size { get; set; }
    public List<string>? Stamp { get; set; }
    public string? Foil { get; set; }
    public Dictionary<string, JsonElement>? ThirdParty { get; set; }
}

public sealed class TcgdexSet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ReleaseDate { get; set; }
    public string? Logo { get; set; }
    public string? Symbol { get; set; }
    public TcgdexCardCount? CardCount { get; set; }
    public TcgdexSerie? Serie { get; set; }
    public TcgdexAbbreviation? Abbreviation { get; set; }
}

public sealed class TcgdexCardCount
{
    public int? Official { get; set; }
    public int? Total { get; set; }
}

public sealed class TcgdexSerie
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
}

public sealed class TcgdexAbbreviation
{
    public string? Official { get; set; }
}

/// <summary>TCGdex returns attack damage as string ("180+") or number (70).</summary>
public sealed class StringOrNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for damage."),
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
