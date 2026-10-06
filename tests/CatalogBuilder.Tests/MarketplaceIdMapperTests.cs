using System.Text.Json;
using CatalogBuilder.Pipeline;
using CatalogBuilder.Sources;

namespace CatalogBuilder.Tests;

public class MarketplaceIdMapperTests
{
    [Fact]
    public void Maps_marketplace_ids_per_print_and_keeps_first_variant_for_merged_prints()
    {
        var card = new TcgdexCard
        {
            Id = "sv03-001",
            VariantsDetailed =
            [
                new() { Type = "normal", Size = "standard", ThirdParty = Ids("""{ "cardmarket": 725081, "tcgplayer": 509637 }""") },
                new() { Type = "reverse", Size = "standard", ThirdParty = Ids("""{ "cardmarket": 725081, "tcgplayer": 509638 }""") },
                new() { Type = "normal", Size = "standard", ThirdParty = Ids("""{ "cardmarket": 1 }""") },
            ],
        };

        var ids = MarketplaceIdMapper.Map(card);

        Assert.Equal(2, ids.Count);
        Assert.Equal("509637", ids.Single(p => p.Key.Finish == "normal").Value["tcgplayer"]);
        Assert.Equal("725081", ids.Single(p => p.Key.Finish == "normal").Value["cardmarket"]);
        Assert.Equal("509638", ids.Single(p => p.Key.Finish == "reverse").Value["tcgplayer"]);
    }

    private static Dictionary<string, JsonElement> Ids(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
}
