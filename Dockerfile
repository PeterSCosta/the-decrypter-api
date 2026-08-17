# Build .NET 10 → runtime aspnet (multi-stage).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore TheDecrypter.slnx
RUN dotnet publish src/TheDecrypter.Api/TheDecrypter.Api.csproj \
    -c Release -o /app /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080

# postgresql-client: o sidecar de seed roda `psql -f schema.sql` em todo deploy.
RUN apt-get update && apt-get install -y --no-install-recommends \
      postgresql-client ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .
COPY db/schema.sql ./db/schema.sql
# Datasets embutidos na imagem para o seed rodar in-cluster sem volume externo.
# Populados pelo CI via `make sync-data` (origem: ../the-decrypter/public/data).
COPY seed-data/municipios.json ./data/municipios.json
COPY seed-data/streets.json    ./data/streets.json
COPY seed-data/ceps.json       ./data/ceps.json
COPY seed-data/postes.json     ./data/postes.json
COPY seed-data/airports.json   ./data/airports.json

EXPOSE 8080
ENTRYPOINT ["dotnet", "TheDecrypter.Api.dll"]
