using Anvil;
using Microsoft.EntityFrameworkCore;

namespace Anvil.Store;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options)
    : AnvilIdentityDbContext<StoreUser>(options)
{
    public DbSet<CreatorProfile> CreatorProfiles => Set<CreatorProfile>();
    public DbSet<StoreListing> Listings => Set<StoreListing>();
    public DbSet<ListingVersion> ListingVersions => Set<ListingVersion>();
    public DbSet<PackageArtifact> PackageArtifacts => Set<PackageArtifact>();
    public DbSet<DownloadRecord> Downloads => Set<DownloadRecord>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<StoreReview> Reviews => Set<StoreReview>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<CreatorProfile>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<StoreListing>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<StoreListing>().HasIndex(x => new { x.PackageType, x.Status });
        modelBuilder.Entity<ListingVersion>().HasIndex(x => new { x.ListingId, x.Version }).IsUnique();
        modelBuilder.Entity<PackageArtifact>().HasIndex(x => x.Sha256).IsUnique();
        modelBuilder.Entity<Favorite>().HasIndex(x => new { x.UserId, x.ListingId }).IsUnique();
        modelBuilder.Entity<StoreReview>().HasIndex(x => new { x.UserId, x.ListingId }).IsUnique();
        modelBuilder.Entity<StoreListing>().Property(x => x.PackageType).HasConversion<string>();
        modelBuilder.Entity<StoreListing>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<ListingVersion>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<PackageArtifact>().Property(x => x.ScanStatus).HasConversion<string>();
    }
}

public enum PackageType { Template, Provider }
public enum ListingStatus { Draft, Published, Withdrawn }
public enum ReleaseStatus { Uploaded, Scanning, ScanFailed, AwaitingReview, ChangesRequested, Approved, Rejected, Published, Withdrawn }
public enum MalwareScanStatus { Pending, Clean, Infected, Unavailable, Failed }

public sealed class CreatorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string? RepositoryUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public StoreUser? User { get; set; }
    public ICollection<StoreListing> Listings { get; set; } = [];
}

public sealed class StoreListing
{
    public int Id { get; set; }
    public int CreatorProfileId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public PackageType PackageType { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Readme { get; set; } = string.Empty;
    public string License { get; set; } = "MIT";
    public string SupportedAnvilVersions { get; set; } = ".NET 10 / Anvil 0.1";
    public string Tags { get; set; } = string.Empty;
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CreatorProfile? Creator { get; set; }
    public ICollection<ListingVersion> Versions { get; set; } = [];
    public ICollection<Favorite> Favorites { get; set; } = [];
    public ICollection<StoreReview> Reviews { get; set; } = [];
}

public sealed class ListingVersion
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string Version { get; set; } = string.Empty;
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Uploaded;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public string? ModeratorNote { get; set; }
    public StoreListing? Listing { get; set; }
    public PackageArtifact? Artifact { get; set; }
}

public sealed class PackageArtifact
{
    public int Id { get; set; }
    public int ListingVersionId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/zip";
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public MalwareScanStatus ScanStatus { get; set; } = MalwareScanStatus.Pending;
    public string? ScanMessage { get; set; }
    public DateTimeOffset? ScannedAt { get; set; }
    public ListingVersion? ListingVersion { get; set; }
}

public sealed class DownloadRecord
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ListingId { get; set; }
    public int ListingVersionId { get; set; }
    public DateTimeOffset DownloadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Favorite
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ListingId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StoreReview
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ListingId { get; set; }
    public int Rating { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ModerationAction
{
    public long Id { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public int ListingVersionId { get; set; }
    public ReleaseStatus Action { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
