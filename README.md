# ClaimManager

A carrier claim handling workbench for managing claims through intake, workflow progression, blocker resolution, approval routing, and search.

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | `dotnet --version` to verify |
| [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling) | latest | `dotnet workload install aspire` |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | any recent | Must be running before starting the app or tests |
| [Node.js](https://nodejs.org/) | 20.19+ or 22.12+ | Required for the frontend |

## Running locally

The AppHost orchestrates everything — PostgreSQL, the API, and the Vite dev server — in one command.

```bash
cd ClaimManager.AppHost
dotnet run
```

Aspire prints a dashboard URL (typically `http://localhost:15888`). The app itself opens at the frontend URL shown in the dashboard under the `webfrontend` resource.

### First run: apply migrations

Before the first run (or after pulling new migrations), apply the database schema:

**Windows (PowerShell):**
```powershell
# Get the connection string from the Aspire dashboard → postgres resource → Connection string
.\scripts\apply-migrations.ps1 -ConnectionString "Host=localhost;Port=61600;Username=postgres;Password=*zWcm0qK!-S9kZ6!43dEt);Database=postgresdb"
```

**macOS / Linux:**
```bash
CONNECTION_STRING="Host=localhost;Port=XXXXX;Database=claimmanager;Username=postgres;Password=postgres" \
  ./scripts/apply-migrations.sh
```

Replace `XXXXX` with the port shown in the Aspire dashboard for the `postgres` resource (it is random on each run unless you pin it).

## Seeded accounts

These accounts are created by migrations and are available immediately after `apply-migrations`:

| Role | Email | Password |
|------|-------|----------|
| Adjuster | adjuster@claimmanager.local | Adjuster!2345 |
| Admin | admin@claimmanager.local | Admin!234567 |

The login form also shows these credentials in the UI for convenience.

## Running tests

### .NET unit and architecture tests (no Docker needed)

```bash
dotnet test tests/ClaimManager.Domain.UnitTests
dotnet test tests/ClaimManager.Application.UnitTests
dotnet test tests/ClaimManager.ArchitectureTests
```

Or all at once:
```bash
dotnet test ClaimManager.sln
```

### Infrastructure integration tests (Docker required)

Uses Testcontainers to spin up a real PostgreSQL instance:

```bash
dotnet test tests/ClaimManager.Infrastructure.IntegrationTests
```

### API functional tests (Docker required)

End-to-end HTTP tests against a real API and database. Uses a single shared Testcontainers PostgreSQL instance across all test classes — tests run serially and take ~30 seconds.

```bash
dotnet test tests/ClaimManager.Api.FunctionalTests
```

> **Known issue:** `Document_upload_without_csrf_header_is_rejected` fails with `InternalServerError` instead of the expected `BadRequest`. This is a pre-existing issue unrelated to the rest of the suite — 21/22 tests pass.

### Frontend unit tests (no Docker needed)

```bash
cd tests/ClaimManager.Frontend.Tests
npm install
npm test
```

### Frontend lint and build check

```bash
cd src/ClaimManager.Frontend
npm install
npm run lint
npm run build
```

## Project structure

```
ClaimManager/
├── ClaimManager.AppHost/          # Aspire orchestration entry point
├── ClaimManager.ServiceDefaults/  # Shared Aspire service configuration
├── src/
│   ├── ClaimManager.Api/          # ASP.NET Core minimal API
│   ├── ClaimManager.Application/  # Commands, validators, DTOs
│   ├── ClaimManager.Domain/       # Claim entity and domain logic
│   ├── ClaimManager.Infrastructure/  # EF Core, Identity, migrations, integrations
│   └── ClaimManager.Frontend/    # React + Vite frontend
├── tests/
│   ├── ClaimManager.Api.FunctionalTests/
│   ├── ClaimManager.Application.UnitTests/
│   ├── ClaimManager.ArchitectureTests/
│   ├── ClaimManager.Domain.UnitTests/
│   ├── ClaimManager.Frontend.Tests/
│   └── ClaimManager.Infrastructure.IntegrationTests/
└── scripts/
    ├── apply-migrations.ps1       # Windows migration helper
    └── apply-migrations.sh        # Unix migration helper
```

## Adding a migration

After changing the domain model or EF Core configuration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/ClaimManager.Infrastructure \
  --startup-project src/ClaimManager.Api
```

Then apply with the migration scripts above, or let the functional tests pick it up automatically (they call `MigrateAsync` on their Testcontainers DB).
