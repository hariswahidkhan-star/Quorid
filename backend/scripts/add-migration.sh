#!/usr/bin/env bash
# Generate an EF Core migration for the Quorid schema.
#
# The design-time factory (Quorid.Infrastructure/Persistence/DesignTimeDbContextFactory.cs)
# lets this run without booting the API or reaching a database. Requires the .NET 8 SDK
# and the EF tools:  dotnet tool install --global dotnet-ef
#
# Usage:  ./scripts/add-migration.sh InitialCreate
set -euo pipefail

NAME="${1:-InitialCreate}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

dotnet ef migrations add "$NAME" \
  --project "$ROOT/src/Quorid.Infrastructure" \
  --startup-project "$ROOT/src/Quorid.Api" \
  --output-dir Persistence/Migrations

echo
echo "Migration '$NAME' created under src/Quorid.Infrastructure/Persistence/Migrations."
echo "Commit it, then apply on startup by setting Database__MigrateOnStartup=true"
echo "(or run: dotnet ef database update --project src/Quorid.Infrastructure --startup-project src/Quorid.Api)."
