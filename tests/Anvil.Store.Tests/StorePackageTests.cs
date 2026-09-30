using Anvil.Store;

namespace Anvil.Store.Tests;

public sealed class StorePackageTests
{
    [Fact]
    public void Manifest_parser_accepts_a_valid_template_contract()
    {
        var result = PackageManifestParser.Parse("""
            {
              "id": "anvil.dashboard",
              "name": "Dashboard",
              "type": "Template",
              "version": "1.2.3",
              "license": "MIT",
              "supportedAnvilVersions": ["0.1"],
              "repositoryUrl": "https://github.com/example/dashboard"
            }
            """);

        Assert.True(result.IsValid);
        Assert.Equal("anvil.dashboard", result.Manifest?.Id);
        Assert.Equal(PackageType.Template, result.Manifest?.Type);
    }

    [Theory]
    [InlineData("../escape", "1.0.0")]
    [InlineData("anvil.dashboard", "not-a-version")]
    public void Manifest_parser_rejects_invalid_identifiers_or_versions(string id, string version)
    {
        var result = PackageManifestParser.Parse("""
            {
              "id": "__ID__",
              "name": "Dashboard",
              "type": "Template",
              "version": "__VERSION__",
              "license": "MIT"
            }
            """.Replace("__ID__", id, StringComparison.Ordinal).Replace("__VERSION__", version, StringComparison.Ordinal));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Manifest_parser_rejects_non_http_repository_urls()
    {
        var result = PackageManifestParser.Parse("""
            {
              "id": "anvil.dashboard",
              "name": "Dashboard",
              "type": "Template",
              "version": "1.0.0",
              "license": "MIT",
              "repositoryUrl": "file:///private/source"
            }
            """);

        Assert.False(result.IsValid);
        Assert.Contains("HTTP", result.Errors.Single());
    }
}
