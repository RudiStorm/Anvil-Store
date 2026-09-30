# Provider Setup

1. Start `appwrite/mailcatcher:1.1.1` with SMTP port `1025` and web port `1080`.
2. Add the provider source to the application.
3. Register `AddAnvilStoreMailcatcher(builder.Configuration)`.
4. Set the `Mailcatcher` configuration section.
5. Send a test message through `AnvilMailer`.
6. Inspect the message at the Mailcatcher web inbox.

Do not use this provider for production delivery. Replace it with a real SMTP
provider before launch.
