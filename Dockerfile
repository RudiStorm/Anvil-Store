FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore Anvil.Store.csproj
RUN dotnet publish Anvil.Store.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY Container/entrypoint.sh /app/entrypoint.sh

RUN chmod +x /app/entrypoint.sh \
    && mkdir -p /app/data/store-storage \
    && groupadd --system --gid 10001 anvil \
    && useradd --system --uid 10001 --gid anvil --home-dir /app --no-create-home anvil \
    && chown -R anvil:anvil /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

ENTRYPOINT ["/app/entrypoint.sh"]
