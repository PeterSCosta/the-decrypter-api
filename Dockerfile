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
COPY --from=build /app .
COPY db/schema.sql ./db/schema.sql
EXPOSE 8080
ENTRYPOINT ["dotnet", "TheDecrypter.Api.dll"]
