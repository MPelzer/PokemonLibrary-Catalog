namespace CatalogBuilder.Format;

// Output model of catalog format v1 (see schema/v1 and docs/catalog-format.md in the app repo).
// Optional members are null and omitted when serialized.

public sealed record Provenance(string Source, string RetrievedAt, string Confidence)
{
    public string? Via { get; init; }
}

public sealed record SetFile(string FormatVersion, CatalogSet Set, IReadOnlyList<CatalogCard> Cards);

public sealed record CatalogSet
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required string Language { get; init; }
    public required string Name { get; init; }
    public string? Series { get; init; }
    public string? Abbreviation { get; init; }
    public string? ReleaseDate { get; init; }
    public CardCount? CardCount { get; init; }
    public Images? Images { get; init; }
    public IReadOnlyDictionary<string, string>? ExternalIds { get; init; }
    public required Provenance Provenance { get; init; }
}

public sealed record CardCount(int? Official, int? Total);

public sealed record Images
{
    public string? Small { get; init; }
    public string? Large { get; init; }
    public string? Logo { get; init; }
    public string? Symbol { get; init; }
}

public sealed record CatalogCard
{
    public required string Id { get; init; }
    public required string Number { get; init; }
    public required string Name { get; init; }
    public string? CardGroup { get; init; }
    public required string Supertype { get; init; }
    public IReadOnlyList<string>? Subtypes { get; init; }
    public int? Hp { get; init; }
    public IReadOnlyList<string>? Types { get; init; }
    public IReadOnlyList<int>? Species { get; init; }
    public string? EvolvesFrom { get; init; }
    public required string Rarity { get; init; }
    public string? Illustrator { get; init; }
    public string? RegulationMark { get; init; }
    public IReadOnlyList<Ability>? Abilities { get; init; }
    public IReadOnlyList<Attack>? Attacks { get; init; }
    public IReadOnlyList<TypeModifier>? Weaknesses { get; init; }
    public IReadOnlyList<TypeModifier>? Resistances { get; init; }
    public int? RetreatCost { get; init; }
    public Images? Images { get; init; }
    public IReadOnlyDictionary<string, string>? ExternalIds { get; init; }
    public required IReadOnlyList<CatalogPrint> Prints { get; init; }
    public required Provenance Provenance { get; init; }
}

public sealed record Ability(string? Kind, string Name, string? Text);

public sealed record Attack(string Name, IReadOnlyList<string>? Cost, string? Damage, string? Text);

public sealed record TypeModifier(string Type, string? Value);

public sealed record CatalogPrint
{
    public required string Id { get; init; }
    public required string Finish { get; init; }
    public string? Edition { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyDictionary<string, string>? ExternalIds { get; init; }
}

public sealed record SpeciesFile(string FormatVersion, IReadOnlyList<Species> Species);

public sealed record Species
{
    public required int Dex { get; init; }
    public int? Generation { get; init; }
    public required IReadOnlyDictionary<string, string> Names { get; init; }
    public IReadOnlyList<string>? Types { get; init; }
    public IReadOnlyList<int>? EvolutionChain { get; init; }
    public IReadOnlyDictionary<string, string>? FlavorText { get; init; }
    public required Provenance Provenance { get; init; }
}

public sealed record Manifest(
    string FormatVersion,
    string CatalogVersion,
    string CreatedAt,
    IReadOnlyList<string> Languages,
    IReadOnlyList<SourceInfo> Sources,
    IReadOnlyList<ManifestFile> Files);

public sealed record SourceInfo(string Id, string Name, IReadOnlyDictionary<string, string> Tiers)
{
    public string? Url { get; init; }
}

public sealed record ManifestFile(string Path, string Kind, string Sha256)
{
    public string? SetId { get; init; }
}
