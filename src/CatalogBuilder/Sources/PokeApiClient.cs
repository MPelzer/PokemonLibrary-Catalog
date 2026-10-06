using System.Text.Json.Nodes;

namespace CatalogBuilder.Sources;

public sealed record PokeApiSpecies(
    int Dex,
    int? Generation,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyDictionary<string, string> FlavorText,
    IReadOnlyList<string> Types,
    IReadOnlyList<int> EvolutionChain);

/// <summary>Species/lore from the PokéAPI GraphQL endpoint (two requests instead of ~3,000 REST calls).</summary>
public sealed class PokeApiClient(Http http)
{
    private const string GraphQlUrl = "https://beta.pokeapi.co/graphql/v1beta";

    public async Task<IReadOnlyList<PokeApiSpecies>> GetSpeciesAsync(IReadOnlyCollection<string> languages, CancellationToken ct)
    {
        var langs = string.Join(",", languages.Append("ja").Distinct().Select(l => $"\"{l}\""));
        var speciesQuery =
            "{ pokemon_v2_pokemonspecies(order_by:{id:asc}) { id generation_id evolution_chain_id " +
            $"names: pokemon_v2_pokemonspeciesnames(where:{{pokemon_v2_language:{{name:{{_in:[{langs}]}}}}}}) {{ name pokemon_v2_language {{ name }} }} " +
            $"flavor: pokemon_v2_pokemonspeciesflavortexts(where:{{pokemon_v2_language:{{name:{{_in:[{langs}]}}}}}}, order_by:{{version_id:desc}}) {{ flavor_text pokemon_v2_language {{ name }} }} " +
            "pokemon_v2_pokemons(where:{is_default:{_eq:true}}) { pokemon_v2_pokemontypes(order_by:{slot:asc}) { pokemon_v2_type { name } } } } }";
        var chainQuery =
            "{ pokemon_v2_evolutionchain { id pokemon_v2_pokemonspecies(order_by:{order:asc}) { id } } }";

        var species = await QueryAsync(speciesQuery, ct);
        var chains = (await QueryAsync(chainQuery, ct))["pokemon_v2_evolutionchain"]!.AsArray()
            .ToDictionary(c => (int)c!["id"]!, c => c!["pokemon_v2_pokemonspecies"]!.AsArray().Select(s => (int)s!["id"]!).ToList());

        return species["pokemon_v2_pokemonspecies"]!.AsArray().Select(s =>
        {
            var names = s!["names"]!.AsArray()
                .GroupBy(n => (string)n!["pokemon_v2_language"]!["name"]!)
                .ToDictionary(g => g.Key, g => (string)g.First()!["name"]!);
            // Newest version first; keep the first text per language, whitespace normalized.
            var flavor = s["flavor"]!.AsArray()
                .GroupBy(f => (string)f!["pokemon_v2_language"]!["name"]!)
                .Where(g => languages.Contains(g.Key))
                .ToDictionary(g => g.Key, g => string.Join(' ', ((string)g.First()!["flavor_text"]!).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
            var types = s["pokemon_v2_pokemons"]!.AsArray().FirstOrDefault()?["pokemon_v2_pokemontypes"]?.AsArray()
                .Select(t => (string)t!["pokemon_v2_type"]!["name"]!).ToList() ?? [];
            var chainId = (int?)s["evolution_chain_id"];
            return new PokeApiSpecies((int)s["id"]!, (int?)s["generation_id"], names, flavor, types,
                chainId is { } id && chains.TryGetValue(id, out var chain) ? chain : []);
        }).ToList();
    }

    private async Task<JsonNode> QueryAsync(string query, CancellationToken ct)
    {
        var json = await http.PostGraphQlAsync(GraphQlUrl, query, ct);
        return json["data"] ?? throw new InvalidOperationException("PokéAPI GraphQL returned no data: " + json["errors"]?.ToJsonString());
    }
}
