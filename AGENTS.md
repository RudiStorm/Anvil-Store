# Anvil Store Agent Guide

## Project Shape

- This is a standalone ASP.NET Core .NET 10 app; the web entrypoint and service wiring are in `Program.cs`, and UI pages are under `Components/Pages`.
- `Anvil.Store.csproj` consumes published `Raukeld.Anvil` and `Raukeld.Anvil.Razor` packages. Do not look for an Anvil framework project reference in this repository.
- The solution contains the app and `tests/Anvil.Store.Tests`; `ExamplePackages/` contains editable sample package sources and is excluded from the main app compilation.
- EF Core persistence is SQLite through `StoreDbContext`; checked-in migrations live in `Migrations/`.

## Commands

- Restore/build: `dotnet build Anvil.Store.slnx`
- Run all tests: `dotnet test Anvil.Store.slnx`
- Run locally with the documented launch profile: `dotnet watch --project Anvil.Store.csproj --launch-profile Anvil.Store`
- Local defaults use SQLite and file storage. `appsettings.Development.json` deliberately sets `Store:MalwareScanner:Required=false`, selecting the development scanner; do not treat that configuration as production-safe.

## Local Services And Deployment

- To inspect development mail, start `docker compose -f Container/docker-compose.mailcatcher.yml up -d` and use `http://localhost:1080`; stop it with the same command and `down`.
- The container starts through `Container/entrypoint.sh`, prepares `/app/data`, drops to the `anvil` user, and listens on `${PORT:-8080}`. Railway health checks `/health/ready`.
- Production requires a persistent volume mounted at `/app/data`; configure `ConnectionStrings__DefaultConnection=Data Source=/app/data/anvil-store.db`, `Store__StorageRoot=/app/data/store-storage`, and `Store__ApplyMigrationsOnStartup=true`.
- Production uploads require ClamAV with `Store__MalwareScanner__Enabled=true` and `Store__MalwareScanner__Required=true`; disabling either explicitly selects the development scanner and bypasses malware verification.

## Data And Migrations

- Startup applies migrations only when `Store:ApplyMigrationsOnStartup` is true, then runs `StoreSeed.SeedAsync`; account roles and initial listings are therefore created by application startup.
- Local database files, `data/`, and `store-storage/` are ignored by Git. Do not commit runtime data or generated `bin/` and `obj/` output.
