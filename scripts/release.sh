#!/usr/bin/env bash
# Builds the catalog, packages it and publishes a GitHub release (requires gh, authenticated).
set -euo pipefail
cd "$(dirname "$0")/.."

base="$(date -u +%Y.%m.%d)"
n=1
while gh release view "catalog-$base.$n" >/dev/null 2>&1; do n=$((n + 1)); done
version="$base.$n"

dotnet run --project src/CatalogBuilder -c Release -- build
dotnet run --project src/CatalogBuilder -c Release -- package --version "$version"

git add catalog
git commit -m "Catalog $version" || echo "No catalog changes to commit."
git push

gh release create "catalog-$version" "dist/catalog-$version.zip" "dist/catalog-$version.zip.sha256" NOTICE.md \
  --title "Catalog $version" \
  --notes "Catalog format v1. Languages: $(jq -r '.languages | join(", ")' catalog/manifest.json). No prices (D27). Sources and licenses: see NOTICE.md."
echo "Released catalog-$version"
