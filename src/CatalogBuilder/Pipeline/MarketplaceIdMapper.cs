using System.Text.Json;
using CatalogBuilder.Normalization;
using CatalogBuilder.Sources;

namespace CatalogBuilder.Pipeline;

/// <summary>
/// Extracts marketplace ids (Cardmarket, TCGplayer, …) per print from a TCGdex REST card.
/// Prices are deliberately not read: the public catalog does not redistribute marketplace prices (D27).
/// </summary>
public static class MarketplaceIdMapper
{
    /// <summary>Marketplace ids per print key (first variant wins when merged prints share a key).</summary>
    public static IReadOnlyDictionary<PrintKey, IReadOnlyDictionary<string, string>> Map(TcgdexCard card)
    {
        var result = new Dictionary<PrintKey, IReadOnlyDictionary<string, string>>();
        foreach (var variant in card.VariantsDetailed ?? [])
        {
            var key = PrintKey.FromVariant(variant.Type, variant.Size, variant.Stamp, variant.Foil);
            if (result.ContainsKey(key) || variant.ThirdParty is not { Count: > 0 } ids) continue;
            result[key] = ids.Where(p => p.Value.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                .ToDictionary(p => p.Key, p => p.Value.ToString());
        }
        return result;
    }
}
