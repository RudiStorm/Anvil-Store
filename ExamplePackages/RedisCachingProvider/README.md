# Anvil Redis Cache Provider

This is an editable Anvil provider package for replacing the default file cache
with Redis in a multi-instance deployment. It is distributed as source through
Anvil Store rather than as an Anvil-specific NuGet package.

## Install

```sh
anvil provider add anvil-team.redis-cache
```

Copy the provider source into the application and add the standard Microsoft
Redis caching package:

```sh
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

Register it after `builder.Services.AddAnvil()`:

```csharp
builder.Services.AddAnvilRedisCaching(builder.Configuration);
```

Configure Redis:

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

The application continues to use Anvil's typed `AnvilCache` API. Redis is only
the backing provider.

## Local Redis

```sh
docker run --name anvil-redis -p 6379:6379 redis:7-alpine
```

## Production

Use a managed Redis service, keep the connection string in a secret, configure
TLS where supported, and use an environment-specific `InstanceName`.
