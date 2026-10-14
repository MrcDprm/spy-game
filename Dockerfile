# Build stage: restore and publish with the full SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project file first, so the restore layer is cached until a dependency changes.
COPY global.json ./
COPY src/SpyGame.Web/SpyGame.Web.csproj src/SpyGame.Web/
RUN dotnet restore src/SpyGame.Web/SpyGame.Web.csproj

COPY src/ src/
RUN dotnet publish src/SpyGame.Web/SpyGame.Web.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Runtime stage: only the ASP.NET runtime, running as the image's non-root "app" user.
# The 10.0 tag always carries the latest patch (10.0.13 fixes CVE-2026-70159 in SignalR);
# the app logs its runtime version at start-up and warns if it is older.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    # Workstation GC keeps memory low on a 512 MB instance.
    DOTNET_gcServer=0
USER $APP_UID
EXPOSE 8080

# Render passes the port to listen on in $PORT.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet SpyGame.Web.dll"]
