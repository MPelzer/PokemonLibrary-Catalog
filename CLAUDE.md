# PokemonLibrary-Catalog

Catalog data + C# pipeline for the PokemonLibrary app (app repo: `../PokemonLibrary`, GitHub `MPelzer/PokemonLibrary`).

- **Contract:** catalog format v1 – `../PokemonLibrary/docs/catalog-format.md`, schema copy in `schema/v1/` (source of truth is the app repo). No shared code with the app (P4, D26).
- **Tasks:** tracked in the app repo's GitHub issues (catalog pipeline: #30, Japanese sets: #31).
- **Build/test:** `dotnet build` · `dotnet test --solution PokemonLibrary.Catalog.sln` · usage in `README.md`.
- **Be polite to sources (NFR-01):** keep bulk GraphQL queries; per-card REST only for marketplace ids, throttled (`requestConcurrency`).
- **Never publish prices (D27):** no `catalog/prices/`, no price fields – marketplace prices are not redistributed; the app fetches them at runtime.
- Unknown source values must be added to `config/vocabulary.json` / `valueMap` – never silently invent keys.
- Conventions as in the app repo: communication with the user in German; code, docs, commits in English.
