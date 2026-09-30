# Anvil Store

Anvil Store is the open-source marketplace for editable Anvil templates and
providers. It is a standalone Anvil application and deploys independently from
the Anvil framework repository.

## Run Locally

```sh
dotnet watch --project Anvil.Store.csproj --launch-profile Anvil.Store
```

The local application uses SQLite and local file storage by default.

## Mailcatcher

Start the development mail service:

```sh
docker compose -f Container/docker-compose.mailcatcher.yml up -d
```

Open the captured inbox at `http://localhost:1080`.
See `MAILCATCHER.md` for configuration and Railway development setup.

## Deployment

Railway uses the root `Dockerfile` and `railway.toml`. Mount the persistent
volume at `/app/data` and configure:

```text
ConnectionStrings__DefaultConnection=Data Source=/app/data/anvil-store.db
Store__StorageRoot=/app/data/store-storage
Store__ApplyMigrationsOnStartup=true
```

See `MALWARE-SCANNER.md` for ClamAV configuration and `docs/caching.md` for
the file-cache default and the downloadable Redis provider.

## Framework Dependency

The application consumes the published `Raukeld.Anvil` and
`Raukeld.Anvil.Razor` packages. The Store repository contains no Anvil source
project references and can be deployed independently.
