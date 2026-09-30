# Anvil Mailcatcher Provider

An application-owned Anvil SMTP provider for local development. It sends mail
to Appwrite Docker Mailcatcher so registration, moderation, and account emails
can be inspected without delivering real messages.

## Setup

```sh
docker run --name anvil-mailcatcher -p 1025:1025 -p 1080:1080 appwrite/mailcatcher:1.1.1
```

Register the provider:

```csharp
builder.Services.AddAnvilStoreMailcatcher(builder.Configuration);
```

Configure it:

```json
{
  "Mailcatcher": {
    "Host": "localhost",
    "Port": 1025,
    "EnableSsl": false
  }
}
```

Open the captured inbox at `http://localhost:1080`.

## Docker Compose

The SMTP service is available at `mailcatcher:1025` from another Compose
container. Use `Mailcatcher__Host=mailcatcher` instead of `localhost`.

This provider is for development only. Use a real TLS-enabled SMTP service in
production.
