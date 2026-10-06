namespace CatalogBuilder.Normalization;

/// <summary>Identity of a print within a card: finish, optional edition, sorted tags (C3: jumbo/stamps/foils are separate prints).</summary>
public sealed record PrintKey(string Finish, string? Edition, IReadOnlyList<string> Tags)
{
    private const string FirstEditionStamp = "1st-edition";

    /// <summary>Derives the key from a TCGdex variant. Values are slugged, so localized casing ("Holo", "Standard") is harmless.</summary>
    public static PrintKey FromVariant(string type, string? size, IEnumerable<string>? stamps, string? foil)
    {
        string? edition = null;
        var tags = new SortedSet<string>(StringComparer.Ordinal);

        var sizeKey = size is null ? "standard" : Keys.Slug(size);
        if (sizeKey != "standard") tags.Add(sizeKey);

        foreach (var stamp in stamps ?? [])
        {
            var stampKey = Keys.Slug(stamp);
            if (stampKey == FirstEditionStamp) edition = "first-edition";
            else tags.Add("stamp-" + stampKey);
        }

        if (!string.IsNullOrWhiteSpace(foil)) tags.Add("foil-" + Keys.Slug(foil));

        return new PrintKey(Keys.Slug(type), edition, [.. tags]);
    }

    public bool Equals(PrintKey? other) =>
        other is not null && Finish == other.Finish && Edition == other.Edition && Tags.SequenceEqual(other.Tags);

    public override int GetHashCode() => HashCode.Combine(Finish, Edition, string.Join('~', Tags));
}
