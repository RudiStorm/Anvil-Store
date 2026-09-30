# Redis Provider Setup

1. Download the provider source from Anvil Store.
2. Add `Microsoft.Extensions.Caching.StackExchangeRedis` to the consuming app.
3. Copy `src/AnvilRedisCachingExtensions.cs` into the application.
4. Configure `ConnectionStrings:Redis`.
5. Call `AddAnvilRedisCaching(builder.Configuration)` after `AddAnvil()`.
6. Keep the file cache for local development and use Redis for multiple app instances.

The provider replaces the `IDistributedCache` backend. Application code should
continue using `AnvilCache`; it should not call StackExchange.Redis directly.
