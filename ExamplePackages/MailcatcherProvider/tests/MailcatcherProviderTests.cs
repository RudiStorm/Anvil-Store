public sealed class MailcatcherProviderTests
{
    public void Provider_defaults_to_local_mailcatcher()
    {
        var settings = new MailcatcherOptions();
        if (settings.Host != "localhost" || settings.Port != 1025 || settings.EnableSsl)
            throw new InvalidOperationException("Unexpected Mailcatcher defaults.");
    }
}
