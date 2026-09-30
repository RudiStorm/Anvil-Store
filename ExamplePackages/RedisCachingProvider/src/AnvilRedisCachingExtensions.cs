using Anvil;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class AnvilRedisCacheOptions
{
    public string? ConnectionStringName { get; set; } = "Redis";
    public string InstanceName { get; set; } = "anvil:";
}

public static class AnvilRedisCachingExtensions
{
    public static IServiceCollection AddAnvilRedisCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("AnvilCache").Get<AnvilRedisCacheOptions>() ?? new();
        var connectionString = configuration.GetConnectionString(settings.ConnectionStringName ?? "Redis");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("A Redis connection string is required.");

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = settings.InstanceName;
        });
        return services;
    }
}
