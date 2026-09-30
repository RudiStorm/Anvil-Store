using Anvil;
using Anvil.Razor;
using Anvil.Store;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<Anvil.Store.StoreOptions>(builder.Configuration.GetSection("Store"));
builder.Services.Configure<AnvilStorageOptions>(options => options.RootPath = Path.Combine(AppContext.BaseDirectory, "data"));
builder.Services.Configure<AnvilCacheOptions>(options => options.Namespace = "store");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "data", "keys")));
builder.Services.AddAnvil();
builder.Services.AddAnvilOpenApi();
builder.Services.AddAnvilReadiness();
builder.Services.AddAnvilCaching(options => options.Namespace = "store");
builder.Services.AddAnvilStoreMailcatcher(builder.Configuration);
builder.Services.Configure<AnvilIdentityOptions>(options =>
{
    options.LoginPath = "/account/login/submit";
    options.RegisterPath = "/account/register/submit";
});
builder.Services.AddAnvilSqlitePersistence<StoreDbContext>(
    builder.Configuration,
    (options, connectionString) => options.UseSqlite(connectionString));
builder.Services.AddAnvilIdentity<StoreUser, StoreDbContext>(identity =>
{
    identity.User.RequireUniqueEmail = true;
    identity.SignIn.RequireConfirmedEmail = false;
}, cookie =>
{
    cookie.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            context.Response.Redirect("/account/login");
        return Task.CompletedTask;
    };
});
builder.Services.AddAnvilIdentityContracts();
builder.Services.AddAnvilAudit<StoreDbContext>();
builder.Services.AddAnvilCompliance();
builder.Services.AddAnvilBackgroundJobs();
builder.Services.AddAnvilTransactionalOutbox<StoreDbContext>();
builder.Services.AddAnvilObservability();
var malwareScannerEnabled = builder.Configuration.GetValue("Store:MalwareScanner:Enabled", true);
var malwareScannerRequired = builder.Configuration.GetValue("Store:MalwareScanner:Required", true);
if (!malwareScannerEnabled || !malwareScannerRequired)
    builder.Services.AddSingleton<IMalwareScanner, DevelopmentMalwareScanner>();
else
    builder.Services.AddSingleton<IMalwareScanner, ClamAvMalwareScanner>();
builder.Services.AddSingleton<IPackageStorage, LocalPackageStorage>();
builder.Services.AddScoped<StoreCatalogReader>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    if (app.Configuration.GetValue<bool>("Store:ApplyMigrationsOnStartup"))
        db.Database.Migrate();

    await StoreSeed.SeedAsync(scope.ServiceProvider, db);
}

app.UseAnvilErrors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAnvil();
app.MapAnvil<Anvil.Store.Components.App>();
app.MapAnvilIdentityEndpoints<StoreUser>();

app.MapAnvilGet("/api/health", _ => Task.FromResult<IResult>(Results.Ok(new { status = "ok" })), "store-health");

app.MapPost("/api/creator/upload", async (RequestContext context, StoreDbContext db, IPackageStorage storage, UserManager<StoreUser> users) =>
{
    var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null)
        return Results.Unauthorized();
    var user = await users.FindByIdAsync(userId);
    if (user is null)
        return Results.Unauthorized();

    var form = await context.ReadFormAsync();
    var file = form.Files.GetFile("package");
    if (file is null)
        return Results.BadRequest(new { error = "Choose a ZIP package." });

    var upload = await storage.ValidateAndQuarantineAsync(file.OpenReadStream(), file.FileName, context.RequestAborted);
    if (!upload.IsValid)
        return Results.BadRequest(new { errors = upload.Errors });
    var manifest = upload.Manifest!;

    var profile = await db.CreatorProfiles.SingleOrDefaultAsync(x => x.UserId == userId, context.RequestAborted);
    if (profile is null)
    {
        var creatorSlug = Slugify(user.UserName ?? user.Email ?? userId);
        profile = new CreatorProfile
        {
            UserId = userId,
            Slug = creatorSlug,
            DisplayName = user.DisplayName ?? user.UserName ?? user.Email ?? "Anvil creator",
            Bio = "An open-source Anvil creator."
        };
        db.CreatorProfiles.Add(profile);
    }
    if (!user.IsCreator)
    {
        user.IsCreator = true;
        await users.UpdateAsync(user);
    }
    if (!await users.IsInRoleAsync(user, "Creator"))
        await users.AddToRoleAsync(user, "Creator");

    var listing = await db.Listings.SingleOrDefaultAsync(x => x.Identifier == manifest.Id, context.RequestAborted);
    if (listing is null)
    {
        listing = new StoreListing
        {
            CreatorProfileId = profile.Id,
            Identifier = manifest.Id,
            Slug = Slugify(manifest.Name),
            Name = manifest.Name,
            PackageType = manifest.Type,
            Summary = form["Summary"].ToString() is { Length: > 0 } summary ? summary : "An open-source Anvil package.",
            Readme = "Documentation is included in the package archive.",
            License = manifest.License,
            SupportedAnvilVersions = string.Join(", ", manifest.SupportedAnvilVersions),
            Status = ListingStatus.Draft
        };
        db.Listings.Add(listing);
    }

    var version = new ListingVersion
    {
        Listing = listing,
        Version = manifest.Version,
        Status = ReleaseStatus.AwaitingReview,
        Artifact = new PackageArtifact
        {
            StorageKey = upload.StorageKey!,
            FileName = file.FileName,
            Length = upload.Length,
            Sha256 = upload.Sha256,
            ScanStatus = MalwareScanStatus.Clean,
            ScanMessage = "ClamAV scan passed or the configured development scanner approved the archive.",
            ScannedAt = DateTimeOffset.UtcNow
        }
    };
    db.ListingVersions.Add(version);
    await db.SaveChangesAsync(context.RequestAborted);
    return Results.Redirect("/creator/submissions");
}).RequireAuthorization();

app.MapPost("/api/admin/releases/{id:int}/approve", async (int id, RequestContext context, StoreDbContext db, IPackageStorage storage, AnvilCache cache) =>
{
    var release = await db.ListingVersions.Include(x => x.Artifact).Include(x => x.Listing).SingleOrDefaultAsync(x => x.Id == id, context.RequestAborted);
    if (release?.Artifact is null || release.Listing is null || release.Artifact.ScanStatus != MalwareScanStatus.Clean)
        return Results.NotFound();

    release.Status = ReleaseStatus.Published;
    release.PublishedAt = DateTimeOffset.UtcNow;
    release.Listing.Status = ListingStatus.Published;
    var publishedKey = $"published/{release.Listing.Identifier}/{release.Version}/{release.Artifact.FileName}";
    await storage.PublishAsync(release.Artifact.StorageKey, publishedKey, context.RequestAborted);
    release.Artifact.StorageKey = publishedKey;
    db.ModerationActions.Add(new ModerationAction
    {
        ActorUserId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
        ListingVersionId = release.Id,
        Action = ReleaseStatus.Published,
        Note = "Approved by moderator."
    });
    await db.SaveChangesAsync(context.RequestAborted);
    await cache.RemoveAsync("public", "catalog:template", context.RequestAborted);
    await cache.RemoveAsync("public", "catalog:provider", context.RequestAborted);
    return Results.Redirect("/admin/submissions");
}).RequireAuthorization(policy => policy.RequireRole("Moderator", "Administrator"));

app.MapPost("/api/admin/releases/{id:int}/reject", async (int id, RequestContext context, StoreDbContext db) =>
{
    var release = await db.ListingVersions.SingleOrDefaultAsync(x => x.Id == id, context.RequestAborted);
    if (release is null)
        return Results.NotFound();
    release.Status = ReleaseStatus.Rejected;
    release.ModeratorNote = context.ReadFormValue("Note")?.Trim();
    db.ModerationActions.Add(new ModerationAction { ActorUserId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", ListingVersionId = id, Action = ReleaseStatus.Rejected, Note = release.ModeratorNote });
    await db.SaveChangesAsync(context.RequestAborted);
    return Results.Redirect("/admin/submissions");
}).RequireAuthorization(policy => policy.RequireRole("Moderator", "Administrator"));

app.MapPost("/api/admin/users/{id}/role", async (string id, RequestContext context, UserManager<StoreUser> users) =>
{
    var user = await users.FindByIdAsync(id);
    if (user is null)
        return Results.NotFound();
    var role = context.ReadFormValue("Role")?.Trim();
    if (role is not ("User" or "Creator" or "Moderator" or "Administrator"))
        return Results.BadRequest(new { error = "Unknown role." });

    var currentRoles = await users.GetRolesAsync(user);
    var removableRoles = currentRoles.Where(x => x is "User" or "Creator" or "Moderator" or "Administrator").ToArray();
    if (removableRoles.Length > 0)
        await users.RemoveFromRolesAsync(user, removableRoles);
    await users.AddToRoleAsync(user, role);
    user.IsCreator = role is "Creator" or "Moderator" or "Administrator";
    await users.UpdateAsync(user);
    return Results.Redirect("/admin");
}).RequireAuthorization(policy => policy.RequireRole("Administrator"));

app.MapPost("/api/admin/mail/test", async (RequestContext context, AnvilMailer mailer) =>
{
    var form = await context.ReadFormAsync();
    var recipient = form["To"].ToString().Trim();
    try { _ = new System.Net.Mail.MailAddress(recipient); }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = "Enter a valid recipient address." });
    }

    await mailer.SendAsync(new AnvilMailMessage(
        "noreply@anvil.store",
        recipient,
        "Anvil Store Mailcatcher test",
        "<h1>Mailcatcher is connected</h1><p>This message was sent by the Anvil Store sample.</p>",
        "Mailcatcher is connected. This message was sent by the Anvil Store sample."), context.RequestAborted);
    return Results.Redirect("/admin/mail?sent=true");
}).RequireAuthorization(policy => policy.RequireRole("Administrator"));

app.MapPost("/api/listings/{id:int}/favorite", async (int id, HttpContext context, StoreDbContext db) =>
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();
    var listing = await db.Listings.FindAsync([id], context.RequestAborted);
    if (listing is null) return Results.NotFound();
    var favorite = await db.Favorites.SingleOrDefaultAsync(x => x.UserId == userId && x.ListingId == id, context.RequestAborted);
    if (favorite is null) db.Favorites.Add(new Favorite { UserId = userId, ListingId = id }); else db.Favorites.Remove(favorite);
    await db.SaveChangesAsync(context.RequestAborted);
    return Results.Redirect($"/{(listing.PackageType == PackageType.Template ? "templates" : "providers")}/{listing.Slug}");
}).RequireAuthorization();

app.MapPost("/api/listings/{id:int}/reviews", async (int id, RequestContext context, StoreDbContext db) =>
{
    var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();
    var listing = await db.Listings.FindAsync([id], context.RequestAborted);
    if (listing is null) return Results.NotFound();
    if (!int.TryParse(context.ReadFormValue("Rating"), out var rating) || rating is < 1 or > 5)
        return Results.BadRequest(new { error = "Rating must be between 1 and 5." });
    if (!await db.Downloads.AnyAsync(x => x.UserId == userId && x.ListingId == id, context.RequestAborted))
        return Results.Forbid();
    var review = await db.Reviews.SingleOrDefaultAsync(x => x.UserId == userId && x.ListingId == id, context.RequestAborted);
    if (review is null) db.Reviews.Add(new StoreReview { UserId = userId, ListingId = id, Rating = rating, Body = context.ReadFormValue("Body")?.Trim() ?? string.Empty });
    else { review.Rating = rating; review.Body = context.ReadFormValue("Body")?.Trim() ?? string.Empty; }
    await db.SaveChangesAsync(context.RequestAborted);
    return Results.Redirect($"/{(listing.PackageType == PackageType.Template ? "templates" : "providers")}/{listing.Slug}");
}).RequireAuthorization();

app.MapGet("/downloads/{id:int}", async (int id, HttpContext context, StoreDbContext db, IPackageStorage storage) =>
{
    var release = await db.ListingVersions.Include(x => x.Listing).Include(x => x.Artifact)
        .SingleOrDefaultAsync(x => x.Id == id && x.Status == ReleaseStatus.Published);
    if (release?.Artifact is null || release.Listing is null)
        return Results.NotFound();

    var stream = await storage.OpenPublishedReadAsync(release.Artifact.StorageKey, context.RequestAborted);
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is not null)
    {
        db.Downloads.Add(new DownloadRecord { UserId = userId, ListingId = release.ListingId, ListingVersionId = release.Id });
        await db.SaveChangesAsync(context.RequestAborted);
    }
    return Results.File(stream, "application/zip", release.Artifact.FileName, enableRangeProcessing: true);
});

app.MapAnvilOpenApi();
app.MapAnvilManifest();
app.MapAnvilReadiness();
app.MapAnvilSitemap("/sitemap.xml", async _ =>
{
    await Task.CompletedTask;
    return (IReadOnlyList<AnvilSitemapEntry>)[new("https://example.test/", DateTimeOffset.UtcNow, "daily", 1.0m), new("https://example.test/templates", DateTimeOffset.UtcNow, "daily", 0.8m), new("https://example.test/providers", DateTimeOffset.UtcNow, "daily", 0.8m)];
});

app.Run();

static string Slugify(string value) => string.Join('-', value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Replace("/", "-");

public partial class Program;

public static class StoreSeed
{
    public static async Task SeedAsync(IServiceProvider services, StoreDbContext db)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<StoreUser>>();
        foreach (var role in new[] { "User", "Creator", "Moderator", "Administrator" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var seededListing = await db.Listings.SingleOrDefaultAsync(x => x.Identifier == "anvil.forge-dashboard");
        if (seededListing is not null)
        {
            seededListing.Identifier = "anvil-team.forge-dashboard";
            await db.SaveChangesAsync();
        }

        if (await db.Listings.AnyAsync())
        {
            await EnsureSeedProviderAsync(db);
            await PromoteFirstRealUserAsync(userManager, db);
            return;
        }

        var admin = await userManager.FindByEmailAsync("admin@anvil.store");
        if (admin is null)
        {
            admin = new StoreUser { UserName = "admin@anvil.store", Email = "admin@anvil.store", EmailConfirmed = true, DisplayName = "Anvil Team", IsCreator = true };
            var bootstrapPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "!aA1";
            var result = await userManager.CreateAsync(admin, bootstrapPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        if (!await userManager.IsInRoleAsync(admin, "Administrator"))
            await userManager.AddToRoleAsync(admin, "Administrator");
        if (!await userManager.IsInRoleAsync(admin, "Creator"))
            await userManager.AddToRoleAsync(admin, "Creator");

        var creator = new CreatorProfile { UserId = admin.Id, Slug = "anvil-team", DisplayName = "Anvil Team", Bio = "Open-source examples maintained by the Anvil team." };
        var listing = new StoreListing
        {
            Creator = creator,
            Slug = "forge-dashboard",
            Identifier = "anvil-team.forge-dashboard",
            Name = "Forge Dashboard",
            PackageType = PackageType.Template,
            Summary = "A polished Identity dashboard starter for Anvil applications.",
            Readme = "A server-rendered dashboard starter built with Razor, EF Core, Identity, and Anvil.",
            License = "MIT",
            SupportedAnvilVersions = ".NET 10 / Anvil 0.1",
            Tags = "dashboard, identity, razor",
            Status = ListingStatus.Published
        };
        db.Listings.Add(listing);
        db.Listings.Add(new StoreListing
        {
            Creator = creator,
            Slug = "redis-cache",
            Identifier = "anvil-team.redis-cache",
            Name = "Anvil Redis Cache Provider",
            PackageType = PackageType.Provider,
            Summary = "An editable Redis-backed IDistributedCache provider for multi-instance Anvil deployments.",
            Readme = "Install the provider source from the Store, add the standard Redis package, and register AddAnvilRedisCaching.",
            License = "MIT",
            SupportedAnvilVersions = ".NET 10 / Anvil 0.1",
            Tags = "redis, caching, distributed, provider",
            Status = ListingStatus.Published
        });
        await db.SaveChangesAsync();
        await EnsureSeedProviderAsync(db);
        await PromoteFirstRealUserAsync(userManager, db);
    }

    private static async Task EnsureSeedProviderAsync(StoreDbContext db)
    {
        if (await db.Listings.AnyAsync(x => x.Identifier == "anvil-team.redis-cache"))
            return;
        var creator = await db.CreatorProfiles.SingleAsync(x => x.Slug == "anvil-team");
        db.Listings.Add(new StoreListing
        {
            CreatorProfileId = creator.Id,
            Slug = "redis-cache",
            Identifier = "anvil-team.redis-cache",
            Name = "Anvil Redis Cache Provider",
            PackageType = PackageType.Provider,
            Summary = "An editable Redis-backed IDistributedCache provider for multi-instance Anvil deployments.",
            Readme = "Install the provider source from the Store, add the standard Redis package, and register AddAnvilRedisCaching.",
            License = "MIT",
            SupportedAnvilVersions = ".NET 10 / Anvil 0.1",
            Tags = "redis, caching, distributed, provider",
            Status = ListingStatus.Published
        });
        await db.SaveChangesAsync();
    }

    private static async Task PromoteFirstRealUserAsync(UserManager<StoreUser> userManager, StoreDbContext db)
    {
        var firstUser = await db.Users
            .Where(x => x.Email != "admin@anvil.store")
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();
        if (firstUser is null || await userManager.IsInRoleAsync(firstUser, "Administrator"))
            return;

        await userManager.AddToRoleAsync(firstUser, "Administrator");
        firstUser.IsCreator = true;
        await userManager.UpdateAsync(firstUser);
        if (!await db.CreatorProfiles.AnyAsync(x => x.UserId == firstUser.Id))
        {
            db.CreatorProfiles.Add(new CreatorProfile
            {
                UserId = firstUser.Id,
                Slug = CreatorSlug(firstUser.UserName ?? firstUser.Email ?? firstUser.Id),
                DisplayName = firstUser.DisplayName ?? firstUser.UserName ?? firstUser.Email ?? "Anvil administrator",
                Bio = "An Anvil Store administrator and creator."
            });
            await db.SaveChangesAsync();
        }
    }

    private static string CreatorSlug(string value) => string.Join('-', value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Replace("/", "-");
}
