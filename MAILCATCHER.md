# Mailcatcher Provider

The Anvil Store sample includes `AddAnvilStoreMailcatcher`, an application-owned
adapter around Anvil's SMTP transport. It sends development mail to
[Appwrite Docker Mailcatcher](https://github.com/appwrite/docker-mailcatcher)
and never delivers messages to real recipients.

## Local Setup

Requirements:

- Docker Desktop or Docker Engine.
- The Anvil Store sample restored and buildable.

From the repository root, start Mailcatcher:

```sh
docker compose -f samples/Anvil.Store/Container/docker-compose.mailcatcher.yml up -d
```

Mailcatcher exposes:

```text
SMTP:  localhost:1025
Inbox: http://localhost:1080
```

Start the Store in another terminal:

```sh
dotnet watch --project samples/Anvil.Store --launch-profile Anvil.Store
```

Sign in as an administrator and open `/admin/mail`. Enter a recipient address
and send the test message, then inspect it at `http://localhost:1080`.

Stop Mailcatcher with:

```sh
docker compose -f samples/Anvil.Store/Container/docker-compose.mailcatcher.yml down
```

Mailcatcher data is disposable. Do not use it for production mail.

## Configuration

The provider reads the `Mailcatcher` section:

```json
{
  "Mailcatcher": {
    "Host": "localhost",
    "Port": 1025,
    "UserName": "",
    "Password": "",
    "EnableSsl": false
  }
}
```

Equivalent environment variables:

```text
Mailcatcher__Host=localhost
Mailcatcher__Port=1025
Mailcatcher__UserName=
Mailcatcher__Password=
Mailcatcher__EnableSsl=false
```

The Store registers it in `Program.cs`:

```csharp
builder.Services.AddAnvilStoreMailcatcher(builder.Configuration);
```

Application code continues to use `AnvilMailer`; it does not depend on
Mailcatcher-specific APIs.

## Docker Networking

If the Store runs in Docker Compose beside Mailcatcher, use the service name:

```text
Mailcatcher__Host=mailcatcher
Mailcatcher__Port=1025
Mailcatcher__EnableSsl=false
```

`localhost` inside the Store container means the Store container itself.

## Railway Development Environment

Create a separate private Railway service using:

```text
appwrite/mailcatcher:1.1.1
```

Make SMTP port `1025` available to the private network. Configure the Store
service with the Mailcatcher private hostname:

```text
Mailcatcher__Host=<mailcatcher-private-hostname>
Mailcatcher__Port=1025
Mailcatcher__EnableSsl=false
```

The web inbox is not automatically public. If exposed for development, protect
it and never use it for customer data.

## Production Boundary

Mailcatcher is development-only. Before production:

- Replace `AddAnvilStoreMailcatcher` with `AddAnvilSmtpMail` and a real provider.
- Enable TLS and certificate validation.
- Store SMTP credentials in Railway secrets.
- Configure a verified sender domain with SPF, DKIM, and DMARC.
- Use a durable outbox and retry policy for transactional messages.
- Do not expose Mailcatcher or its inbox publicly.
- Keep passwords, reset tokens, and sensitive data out of logs and test emails.
