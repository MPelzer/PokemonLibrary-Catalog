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

## Adding a language

Add the ISO code to `languages` in `config/pipeline.json` and run `build`. International languages (fr, it, es, pt, …) work like `de`. Japanese has its own sets and numbering and needs extra work (app repo issue #31).

## Licenses and attribution

See [`NOTICE.md`](NOTICE.md). Card data comes from TCGdex (MIT), species data from PokéAPI (BSD-3-Clause).
This project is not produced, endorsed, supported or affiliated with Nintendo, Creatures, GAME FREAK or The Pokémon Company.
