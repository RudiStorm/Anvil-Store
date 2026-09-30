using Anvil;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Anvil.Store;

public sealed record StoreCatalogItem(
    int Id,
    string Slug,
    string Name,
    PackageType PackageType,
    string Summary,
    string License,
    string SupportedAnvilVersions,
    string Tags);

public sealed class StoreCatalogReader(StoreDbContext db, AnvilCache cache)
{
    public Task<IReadOnlyList<StoreCatalogItem>> GetPublishedAsync(PackageType packageType, CancellationToken cancellationToken = default)
    {
        var key = $"catalog:{packageType.ToString().ToLowerInvariant()}";
        return GetOrCreateAsync(key, packageType, cancellationToken);
    }

    private async Task<IReadOnlyList<StoreCatalogItem>> GetOrCreateAsync(string key, PackageType packageType, CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync("public", key, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<List<StoreCatalogItem>>(cached) ?? [];

        var value = (IReadOnlyList<StoreCatalogItem>)await db.Listings
                .AsNoTracking()
                .Where(x => x.Status == ListingStatus.Published && x.PackageType == packageType)
                .OrderByDescending(x => x.Id)
                .Select(x => new StoreCatalogItem(x.Id, x.Slug, x.Name, x.PackageType, x.Summary, x.License, x.SupportedAnvilVersions, x.Tags))
                .ToListAsync(cancellationToken);
        await cache.SetAsync("public", key, JsonSerializer.SerializeToUtf8Bytes(value), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        }, cancellationToken);
        return value;
    }
}
