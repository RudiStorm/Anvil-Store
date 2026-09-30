# Anvil Data Caching

Anvil registers a file-backed `IDistributedCache` and the typed `AnvilCache`
facade by default. This is suitable for local development and a single-node
deployment. The database remains the source of truth; cache failures must fall
back to normal application reads.

## Default File Cache

`AddAnvil()` enables the file provider automatically:

```csharp
builder.Services.AddAnvil();
```

Configure its storage root when the application has a persistent data volume:

```csharp
builder.Services.Configure<AnvilStorageOptions>(options =>
{
    options.RootPath = "/app/data";
});
```

Use `AnvilCache` for typed cache-aside reads:

```csharp
var value = await cache.GetOrCreateAsync(
    "public",
    "catalog:templates",
    async cancellationToken => await LoadTemplatesAsync(cancellationToken),
    new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
    },
    cancellationToken);
```

The first key segment is a scope. Do not put authenticated or user-owned data
in a public scope.

## Redis Provider

Redis is distributed as editable source through Anvil Store, not as a mandatory
Anvil core dependency. The Store package is:

```text
anvil-team.redis-cache
```

Install the source package through the Store, add the standard Microsoft Redis
package to the consuming application, copy the provider source, and register:

```csharp
builder.Services.AddAnvilRedisCaching(builder.Configuration);
```

Configure:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "AnvilCache": {
    "InstanceName": "anvil:my-app:"
  }
}
```

Redis is recommended when multiple application instances share cache data.

## Rules

- Cache DTOs, not EF entities.
- Use request memoization before shared caching.
- Keep TTLs short for mutable public data.
- Invalidate after a committed mutation.
- Never cache account, role, moderation, upload, or signed-download responses.
- Treat cache data as disposable.
- Version cache keys when serialized DTO shapes change.
- Do not make application correctness depend on cache availability.
