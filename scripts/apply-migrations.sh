#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: ./scripts/apply-migrations.sh '<connection-string>'"
  exit 1
fi

export ConnectionStrings__postgresdb="$1"

dotnet tool run dotnet-ef database update \
  --project src/ClaimManager.Infrastructure/ClaimManager.Infrastructure.csproj \
  --startup-project src/ClaimManager.Api/ClaimManager.Api.csproj