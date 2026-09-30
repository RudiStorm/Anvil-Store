using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Anvil.Store;

public sealed class StoreOptions
{
    public string StorageRoot { get; set; } = "store-storage";
    public MalwareScannerOptions MalwareScanner { get; set; } = new();
}

public sealed class MalwareScannerOptions
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public bool Required { get; set; } = true;
}

public sealed record PackageUploadResult(
    bool IsValid,
    string? StorageKey,
    long Length,
    string Sha256,
    PackageManifest? Manifest,
    IReadOnlyList<string> Errors);

public interface IPackageStorage
{
    Task<PackageUploadResult> ValidateAndQuarantineAsync(Stream source, string fileName, CancellationToken cancellationToken);
    Task PublishAsync(string quarantineKey, string publishedKey, CancellationToken cancellationToken);
    Task<Stream> OpenPublishedReadAsync(string storageKey, CancellationToken cancellationToken);
}

public interface IMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(Stream source, CancellationToken cancellationToken);
}

public sealed record MalwareScanResult(MalwareScanStatus Status, string Message)
{
    public bool IsClean => Status == MalwareScanStatus.Clean;
}

public sealed class DevelopmentMalwareScanner : IMalwareScanner
{
    public Task<MalwareScanResult> ScanAsync(Stream source, CancellationToken cancellationToken)
        => Task.FromResult(new MalwareScanResult(MalwareScanStatus.Clean, "Development scanner approved the archive; production must use ClamAV."));
}

public sealed class ClamAvMalwareScanner(
    IOptions<StoreOptions> options,
    ILogger<ClamAvMalwareScanner> logger) : IMalwareScanner
{
    public async Task<MalwareScanResult> ScanAsync(Stream source, CancellationToken cancellationToken)
    {
        var scanner = options.Value.MalwareScanner;
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(scanner.Host, scanner.Port, cancellationToken);
            await using var network = client.GetStream();
            await network.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), cancellationToken);
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                var length = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(read));
                await network.WriteAsync(length, cancellationToken);
                await network.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await network.WriteAsync(new byte[4], cancellationToken);
            var response = await new StreamReader(network).ReadToEndAsync(cancellationToken);
            return response.Contains("OK", StringComparison.OrdinalIgnoreCase)
                ? new(MalwareScanStatus.Clean, "ClamAV reported no threats.")
                : new(MalwareScanStatus.Infected, response.Trim());
        }
        catch (Exception exception) when (exception is IOException or System.Net.Sockets.SocketException or OperationCanceledException)
        {
            logger.LogWarning(exception, "ClamAV was unavailable.");
            return new(MalwareScanStatus.Unavailable, "The malware scanner is unavailable.");
        }
    }
}

public sealed class LocalPackageStorage(
    IOptions<StoreOptions> options,
    IMalwareScanner scanner,
    ILogger<LocalPackageStorage> logger) : IPackageStorage
{
    private static readonly string[] AllowedDirectories = ["src", "tests", "docs", "screenshots", "examples"];

    private string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.Value.StorageRoot));

    public async Task<PackageUploadResult> ValidateAndQuarantineAsync(Stream source, string fileName, CancellationToken cancellationToken)
    {
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return new(false, null, 0, string.Empty, null, ["Only ZIP archives are accepted."]);

        Directory.CreateDirectory(Path.Combine(Root, "quarantine"));
        var key = $"quarantine/{Guid.NewGuid():N}.zip";
        var path = GetSafePath(key);
        await using (var output = File.Create(path))
            await source.CopyToAsync(output, cancellationToken);

        var length = new FileInfo(path).Length;
        if (length is <= 0 or > 50 * 1024 * 1024)
            return InvalidUpload(path, "The archive must be between 1 byte and 50 MB.");

        await using var scanStream = File.OpenRead(path);
        var scan = await scanner.ScanAsync(scanStream, cancellationToken);
        if (!scan.IsClean)
            return InvalidUpload(path, scan.Message, scan.Status);

        PackageValidationResult validation;
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var entries = archive.Entries;
            if (entries.Count > 10_000)
                return InvalidUpload(path, "The archive contains too many files.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var normalized = entry.FullName.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') || normalized.Contains("../") || normalized.Contains("..\\"))
                    return InvalidUpload(path, "The archive contains an unsafe path.");
                if (!names.Add(normalized))
                    return InvalidUpload(path, "The archive contains duplicate paths.");
                if (entry.ExternalAttributes != 0 && (entry.ExternalAttributes & 0xF000) == 0xA000)
                    return InvalidUpload(path, "Symbolic links are not allowed.");
                var top = normalized.Split('/')[0];
                if (normalized.Contains('/') && top is not ("src" or "tests" or "docs" or "screenshots" or "examples"))
                    return InvalidUpload(path, $"The archive contains an unsupported top-level directory: {top}.");
                if (entry.Length > 10 * 1024 * 1024)
                    return InvalidUpload(path, "An individual file exceeds 10 MB.");
            }

            var manifestEntry = entries.FirstOrDefault(x => string.Equals(x.FullName, "anvil-package.json", StringComparison.OrdinalIgnoreCase));
            var readmeEntry = entries.FirstOrDefault(x => string.Equals(x.FullName, "README.md", StringComparison.OrdinalIgnoreCase));
            var licenseEntry = entries.FirstOrDefault(x => string.Equals(x.FullName, "LICENSE", StringComparison.OrdinalIgnoreCase));
            if (manifestEntry is null || readmeEntry is null || licenseEntry is null)
                return InvalidUpload(path, "The archive must include anvil-package.json, README.md, and LICENSE.");

            using var reader = new StreamReader(manifestEntry.Open());
            validation = PackageManifestParser.Parse(reader.ReadToEnd());
            if (!validation.IsValid)
                return InvalidUpload(path, string.Join(" ", validation.Errors));
        }
        catch (InvalidDataException)
        {
            return InvalidUpload(path, "The upload is not a valid ZIP archive.");
        }

        await using var hashStream = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken)).ToLowerInvariant();
        logger.LogInformation("Validated package {PackageId} version {Version} as {StorageKey}.", validation.Manifest!.Id, validation.Manifest.Version, key);
        return new(true, key, length, hash, validation.Manifest, []);
    }

    public Task PublishAsync(string quarantineKey, string publishedKey, CancellationToken cancellationToken)
    {
        var source = GetSafePath(quarantineKey);
        var destination = GetSafePath(publishedKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination);
        return Task.CompletedTask;
    }

    public Task<Stream> OpenPublishedReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = GetSafePath(storageKey);
        if (!File.Exists(path))
            throw new FileNotFoundException("The package artifact was not found.", storageKey);
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    private string GetSafePath(string key)
    {
        var path = Path.GetFullPath(Path.Combine(Root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The storage key escapes the storage root.");
        return path;
    }

    private PackageUploadResult InvalidUpload(string path, string error, MalwareScanStatus? scanStatus = null)
    {
        try { File.Delete(path); } catch { }
        return new(false, null, 0, string.Empty, null, [error]);
    }
}
