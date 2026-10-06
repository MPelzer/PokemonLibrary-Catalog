using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatalogBuilder.Sources;

/// <summary>
/// TCGdex access. Bulk card metadata comes from one GraphQL request per language (language via the
/// <c>@locale</c> directive); marketplace ids are only available per card via REST.
/// </summary>
public sealed class TcgdexClient(Http http)
{
    private const string GraphQlUrl = "https://api.tcgdex.net/v2/graphql";
    private const string RestUrl = "https://api.tcgdex.net/v2";

    private const string CardFields =
        "id localId name category rarity illustrator hp types dexId stage suffix evolveFrom regulationMark retreat image " +
        "trainerType energyType attacks { name cost damage effect } abilities { type name effect } " +
        "weaknesses { type value } resistances { type value } variants_detailed { type size stamp foil } set { id }";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<TcgdexSet>> GetSetsAsync(string lang, CancellationToken ct)
    {
        var (data, _) = await QueryAsync(
            $"{{ sets @locale(lang: \"{lang}\") {{ id name releaseDate logo symbol cardCount {{ official total }} serie {{ id name }} }} }}", ct);
        return data["sets"].Deserialize<List<TcgdexSet>>(JsonOptions) ?? [];
    }

    public async Task<TcgdexAbbreviation?> GetSetAbbreviationAsync(string lang, string setId, CancellationToken ct)
    {
        var json = await http.GetJsonAsync($"{RestUrl}/{lang}/sets/{Uri.EscapeDataString(setId)}", ct);
        return json["abbreviation"].Deserialize<TcgdexAbbreviation>(JsonOptions);
    }

    /// <summary>
    /// All cards of a language. GraphQL returns partial results with errors for a few malformed source
    /// records; those cards are fetched again via REST.
    /// </summary>
    public async Task<IReadOnlyList<TcgdexCard>> GetCardsAsync(string lang, CancellationToken ct)
    {
        var (data, errorPaths) = await QueryAsync($"{{ cards @locale(lang: \"{lang}\") {{ {CardFields} }} }}", ct);
        var cards = data["cards"].Deserialize<List<TcgdexCard>>(JsonOptions) ?? [];

        var broken = errorPaths.Where(p => p.Count > 1 && p[0] == "cards" && int.TryParse(p[1], out _))
            .Select(p => int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)).Distinct().ToList();
        foreach (var index in broken)
        {
            if (index >= cards.Count) continue;
            try
            {
                cards[index] = await GetCardAsync(lang, cards[index].Id, ct);
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"  warning: could not refetch {lang}/{cards[index].Id}: {ex.Message}");
            }
        }
        return cards;
    }

    /// <summary>Full card incl. variant pricing and marketplace ids (REST).</summary>
    public async Task<TcgdexCard> GetCardAsync(string lang, string cardId, CancellationToken ct)
    {
        // Some TCGdex ids are already percent-encoded (e.g. "exu-%3F"); do not encode them twice.
        var idPart = cardId.Contains('%', StringComparison.Ordinal) ? cardId : Uri.EscapeDataString(cardId);
        var json = await http.GetJsonAsync($"{RestUrl}/{lang}/cards/{idPart}", ct);
        return json.Deserialize<TcgdexCard>(JsonOptions) ?? throw new JsonException($"Empty card {cardId}");
    }

    private async Task<(JsonNode Data, List<List<string>> ErrorPaths)> QueryAsync(string query, CancellationToken ct)
    {
        var json = await http.PostGraphQlAsync(GraphQlUrl, query, ct);
        var errorPaths = (json["errors"]?.AsArray() ?? [])
            .Select(e => (e?["path"]?.AsArray() ?? []).Select(p => p?.ToString() ?? "").ToList())
            .ToList();
        var data = json["data"] ?? throw new JsonException("TCGdex GraphQL returned no data: " + json["errors"]?.ToJsonString());
        return (data, errorPaths);
    }
}
