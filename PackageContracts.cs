using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anvil.Store;

public sealed class PackageManifest
{
    [Required, MinLength(3), MaxLength(120), RegularExpression(@"^[a-z0-9]+([.-][a-z0-9]+)*$")]
    public string Id { get; set; } = string.Empty;
    [Required, MinLength(2), MaxLength(160)]
    public string Name { get; set; } = string.Empty;
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PackageType Type { get; set; }
    [Required, RegularExpression(@"^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$")]
    public string Version { get; set; } = string.Empty;
    [Required, MinLength(1)]
    public string License { get; set; } = string.Empty;
    public string[] SupportedAnvilVersions { get; set; } = [];
    public string[] SupportedDotnetVersions { get; set; } = [];
    public string[] Dependencies { get; set; } = [];
    public string? RepositoryUrl { get; set; }
    public string? Installation { get; set; }
    public string? Customization { get; set; }
}

public sealed record PackageValidationResult(bool IsValid, IReadOnlyList<string> Errors, PackageManifest? Manifest)
{
    public static PackageValidationResult Invalid(params string[] errors) => new(false, errors, null);
}

public static class PackageManifestParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static PackageValidationResult Parse(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<PackageManifest>(json, Options);
            if (manifest is null)
                return PackageValidationResult.Invalid("The manifest is empty.");

            var context = new ValidationContext(manifest);
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(manifest, context, results, true))
                return new(false, results.Select(x => x.ErrorMessage ?? "Invalid manifest.").ToArray(), null);

            if (!string.IsNullOrWhiteSpace(manifest.RepositoryUrl))
            {
                if (!Uri.TryCreate(manifest.RepositoryUrl, UriKind.Absolute, out var repository)
                    || repository.Scheme is not ("https" or "http"))
                    return PackageValidationResult.Invalid("RepositoryUrl must be an HTTP or HTTPS URL.");
            }

            return new(true, [], manifest);
        }
        catch (JsonException exception)
        {
            return PackageValidationResult.Invalid($"The manifest is not valid JSON: {exception.Message}");
        }
    }
}
