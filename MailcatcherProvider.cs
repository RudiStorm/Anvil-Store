using Anvil;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Store;

public sealed class MailcatcherOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; }
}

public static class MailcatcherServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilStoreMailcatcher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection("Mailcatcher").Get<MailcatcherOptions>() ?? new();
        services.AddAnvilSmtpMail(options =>
        {
            options.SmtpHost = settings.Host;
            options.SmtpPort = settings.Port;
            options.SmtpUserName = settings.UserName;
            options.SmtpPassword = settings.Password;
            options.SmtpEnableSsl = settings.EnableSsl;
        });
        return services;
    }
}
