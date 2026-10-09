namespace CatalogBuilder.Normalization;

/// <summary>Identity of a print within a card: finish, optional edition, sorted tags (C3: jumbo/stamps/foils are separate prints).</summary>
public sealed record PrintKey(string Finish, string? Edition, IReadOnlyList<string> Tags)
{
    private const string FirstEditionStamp = "1st-edition";
    private const string StampPrefix = "stamp-";
    private const string FoilPrefix = "foil-";

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
            else tags.Add(StampPrefix + stampKey);
        }

        if (!string.IsNullOrWhiteSpace(foil)) tags.Add(FoilPrefix + Keys.Slug(foil));

        return new PrintKey(Keys.Slug(type), edition, [.. tags]);
    }

    /// <summary>
    /// The language-independent form for the print's <c>edition</c> and <c>tags</c> (catalog issue #1): localized stamp
    /// and foil values are translated (e.g. <c>kosmos</c> → <c>cosmos</c>), and a stamp that means 1st edition sets the
    /// edition. The print ID keeps using the source key, so published IDs never change (FR-CAT-10).
    /// </summary>
    public PrintKey Normalize(Func<string, string> stamp, Func<string, string> foil)
    {
        var edition = Edition;
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var tag in Tags)
        {
            if (tag.StartsWith(StampPrefix, StringComparison.Ordinal))
            {
                var value = stamp(tag[StampPrefix.Length..]);
                if (value == FirstEditionStamp) edition = "first-edition";
                else tags.Add(StampPrefix + value);
            }
            else if (tag.StartsWith(FoilPrefix, StringComparison.Ordinal)) tags.Add(FoilPrefix + foil(tag[FoilPrefix.Length..]));
            else tags.Add(tag);
        }
        return new PrintKey(Finish, edition, [.. tags]);
    }

    public bool Equals(PrintKey? other) =>
        other is not null && Finish == other.Finish && Edition == other.Edition && Tags.SequenceEqual(other.Tags);

    public override int GetHashCode() => HashCode.Combine(Finish, Edition, string.Join('~', Tags));
}
