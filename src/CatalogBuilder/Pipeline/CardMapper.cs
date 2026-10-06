using CatalogBuilder.Format;
using CatalogBuilder.Normalization;
using CatalogBuilder.Sources;

namespace CatalogBuilder.Pipeline;

/// <summary>
/// Maps TCGdex cards to catalog cards. For non-reference languages the enumerations (rarity, types,
/// stage, …) are taken from the reference-language record with the same TCGdex id, because the
/// localized values are inconsistent in the source; texts come from the language's own record.
/// </summary>
public sealed class CardMapper(Vocabulary vocabulary, string retrievedAt)
{
    public CatalogCard Map(string lang, string setCode, TcgdexCard card, TcgdexCard? reference)
    {
        var enums = reference ?? card;
        var setId = Ids.Set(lang, setCode);
        var cardId = Ids.Card(setId, card.LocalId);
        var ctx = $"{lang}/{card.Id}";

        var subtypes = new List<string>();
        AddIfNotNull(subtypes, vocabulary.Map("stage", "subtypes", enums.Stage, ctx));
        AddIfNotNull(subtypes, vocabulary.Map("suffix", "subtypes", enums.Suffix, ctx));
        AddIfNotNull(subtypes, vocabulary.Map("trainerType", "subtypes", enums.TrainerType, ctx));
        AddIfNotNull(subtypes, vocabulary.Map("energyType", "subtypes", enums.EnergyType, ctx));

        var prints = MapPrints(cardId, card.VariantsDetailed);

        return new CatalogCard
        {
            Id = cardId,
            Number = card.LocalId,
            Name = card.Name,
            CardGroup = Ids.InternationalGroup(setCode, card.LocalId),
            Supertype = vocabulary.Map("category", "supertypes", enums.Category, ctx) ?? "pokemon",
            Subtypes = subtypes.Count > 0 ? subtypes.Distinct().ToList() : null,
            Hp = card.Hp,
            Types = vocabulary.MapAll("types", "types", enums.Types, ctx),
            Species = card.DexId is { Count: > 0 } ? card.DexId : null,
            EvolvesFrom = card.EvolveFrom,
            Rarity = vocabulary.Map("rarity", "rarities", enums.Rarity ?? "None", ctx) ?? "none",
            Illustrator = card.Illustrator,
            RegulationMark = card.RegulationMark,
            Abilities = MapAbilities(card.Abilities, enums.Abilities),
            Attacks = MapAttacks(card.Attacks, enums.Attacks, ctx),
            Weaknesses = MapModifiers(enums.Weaknesses, ctx),
            Resistances = MapModifiers(enums.Resistances, ctx),
            RetreatCost = card.Retreat,
            Images = card.Image is null ? null : new Images { Small = card.Image + "/low.webp", Large = card.Image + "/high.webp" },
            ExternalIds = new Dictionary<string, string> { ["tcgdex"] = card.Id },
            Prints = prints,
            Provenance = new Provenance("tcgdex", retrievedAt, "high"),
        };
    }

    /// <summary>Distinct prints; variants that cannot be distinguished by attributes are merged (C3).</summary>
    public static IReadOnlyList<CatalogPrint> MapPrints(string cardId, IEnumerable<TcgdexVariant>? variants)
    {
        var keys = (variants ?? []).Select(v => PrintKey.FromVariant(v.Type, v.Size, v.Stamp, v.Foil)).Distinct().ToList();
        if (keys.Count == 0) keys.Add(new PrintKey("normal", null, []));
        return keys.Select(k => new CatalogPrint
        {
            Id = Ids.Print(cardId, k),
            Finish = k.Finish,
            Edition = k.Edition,
            Tags = k.Tags.Count > 0 ? k.Tags : null,
        }).ToList();
    }

    /// <summary>Ability kind ("ability", "poke-power", …) is an enumeration → taken from the reference record by position.</summary>
    private static List<Ability>? MapAbilities(List<TcgdexAbility?>? abilities, List<TcgdexAbility?>? referenceAbilities)
    {
        var own = (abilities ?? []).OfType<TcgdexAbility>().Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();
        var reference = (referenceAbilities ?? []).OfType<TcgdexAbility>().Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();
        var result = own.Select((a, i) =>
        {
            var kind = reference.Count == own.Count ? reference[i].Type : a.Type;
            return new Ability(kind is null ? null : Keys.Slug(kind), a.Name!, a.Effect);
        }).ToList();
        return result.Count > 0 ? result : null;
    }

    private List<Attack>? MapAttacks(List<TcgdexAttack?>? attacks, List<TcgdexAttack?>? referenceAttacks, string ctx)
    {
        var own = (attacks ?? []).OfType<TcgdexAttack>().Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();
        var reference = (referenceAttacks ?? []).OfType<TcgdexAttack>().Where(a => !string.IsNullOrWhiteSpace(a.Name)).ToList();
        var sameShape = reference.Count == own.Count;

        var result = own.Select((a, i) => new Attack(
            a.Name!,
            vocabulary.MapAll("types", "types", sameShape ? reference[i].Cost : a.Cost, ctx),
            string.IsNullOrWhiteSpace(a.Damage) ? null : a.Damage,
            string.IsNullOrWhiteSpace(a.Effect) ? null : a.Effect)).ToList();
        return result.Count > 0 ? result : null;
    }

    private List<TypeModifier>? MapModifiers(List<TcgdexTypeModifier?>? modifiers, string ctx)
    {
        var result = (modifiers ?? []).OfType<TcgdexTypeModifier>()
            .Select(m => (Type: vocabulary.Map("types", "types", m.Type, ctx), m.Value))
            .Where(m => m.Type is not null)
            .Select(m => new TypeModifier(m.Type!, m.Value)).ToList();
        return result.Count > 0 ? result : null;
    }

    private static void AddIfNotNull(List<string> list, string? value)
    {
        if (value is not null) list.Add(value);
    }
}
