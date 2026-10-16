# PokemonLibrary-Catalog

Card catalog for [PokemonLibrary](https://github.com/MPelzer/PokemonLibrary) and the pipeline that builds it (decisions D8, D26).

- `catalog/` – the catalog in **format v1** (one file per language-specific set, species, vocabulary). Committed, so Git is the history.
  It contains **no prices**: marketplace prices are not redistributed (decision D27); the app fetches them at runtime.
- `src/CatalogBuilder/` – C# console app that fetches the sources, normalizes them and validates the result.
- `schema/v1/` – copy of the JSON Schema (source of truth: app repo, `docs/catalog-format/`). It is the **only contract** with the app (P4).
- `config/` – languages, excluded series, value mappings (`pipeline.json`), source registry with tiers (`sources.json`) and the curated vocabulary with display names (`vocabulary.json`).

## Sources

| Data | Source | Tier | Requests per run |
|---|---|---|---|
| Card metadata + texts (per language) | TCGdex GraphQL | S2 | 1 per language |
| Set details (abbreviation) | TCGdex REST | S2 | ~1 per set and language |
| Marketplace ids (Cardmarket, TCGplayer) | TCGdex REST | S2 | 1 per card, reference language only, 4 in parallel |
| Species / lore | PokéAPI GraphQL | S2 | 2 |
| Cross-language groups for Japanese cards | derived by the pipeline (`catalog-pipeline`) | S5 | – |

Pokémon TCG Pocket (digital) is excluded (`excludedSeries`). Prices in the per-card responses are ignored; `package` refuses to run if a `catalog/prices/` folder exists.

## Usage

```bash
dotnet run --project src/CatalogBuilder -c Release -- build                       # full build incl. marketplace ids (~15 min)
dotnet run --project src/CatalogBuilder -c Release -- build --no-marketplace-ids  # metadata only (~30 s), keeps existing marketplace ids
dotnet run --project src/CatalogBuilder -c Release -- validate                    # validate catalog/ against the schema
dotnet run --project src/CatalogBuilder -c Release -- package                     # write manifest + dist/catalog-<version>.zip(.sha256)
scripts/release.sh                                                                # build, package, commit, GitHub release
```

The build **fails** when a source delivers a value that is not in the vocabulary (e.g. a new rarity). Then add it to
`config/vocabulary.json` (key, display names, rank) or map it in `config/pipeline.json` → `valueMap`, and run again.
`--lenient` builds anyway (unknown values fall back to `none` / are omitted).

## Normalization rules (summary)

- IDs: `<lang>/<setCode>/<number>/<finish>[.<edition>][~<tag>]…`; set code = TCGdex id lower-case with `.` → `-`.
- Enumerations (rarity, types, stage, …) of non-reference languages are taken from the **English record with the same TCGdex id** – the localized source values are inconsistent. Texts come from the language's own record.
- Jumbo, stamps and foil patterns become separate prints via tags (`~jumbo`, `~stamp-staff`, `~foil-cosmos`); the `1st-edition` stamp becomes the edition. Variants without distinguishing attributes are merged.
- Every finish and tag must be in `config/vocabulary.json` (`finishes`, `variantTags` with names and kind – format 1.1,
  app decision D60), otherwise the build fails like for other unknown values. Localized or misspelled source values are
  mapped in `valueMap` (`finish`, `foil`, `stamp`); a foil mapped to `stamp-…` is a distribution mark and becomes a stamp
  (`league` → `stamp-league`). Print IDs keep the source form (`…/reverse~foil-league`, `…/metall`).
- The build log warns about prints that became equal after normalization and – as a hint for source errors (D58) –
  about reference cards whose regular finishes differ from the usual pattern of their set and rarity (e.g. a normal
  print of an SV rare). These are checks only; the data stays as the source delivers it.

## Adding a language

Add the ISO code to `languages` in `config/pipeline.json` and run `build`. International languages (fr, it, es, pt, …) work like `de`.

**Japanese** (`ja`, app decision D53) has its own sets and numbering, so it is listed in `separateNumbering` too:
- its enumerations come from its own records (TCGdex delivers them in English); a missing species is derived from the
  longest Japanese species name (PokéAPI) contained in the card name;
- `CrossLanguageMatcher` puts a card into the `intl/…` group of its English counterpart – Pokémon by species, HP,
  illustrator, attack damage and types, versions of the same card in number order, trainers/energy one-to-one by
  illustrator and subtype within the counterpart set – with `fieldProvenance.cardGroup` (`catalog-pipeline`, S5,
  confidence high/medium/low). Unmatched cards stay ungrouped (~8,100 of ~12,300 matched in 2026-10).
- Gaps (images, abbreviations, untranslated names) are **not** filled from other sources: pokemon-card.com forbids
  reusing its content, other sites grant no reuse (NFR-01). The app shows the counterpart's image instead (D53).

## Licenses and attribution

See [`NOTICE.md`](NOTICE.md). Card data comes from TCGdex (MIT), species data from PokéAPI (BSD-3-Clause).
This project is not produced, endorsed, supported or affiliated with Nintendo, Creatures, GAME FREAK or The Pokémon Company.
