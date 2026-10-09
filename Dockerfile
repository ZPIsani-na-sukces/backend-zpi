# SDK pinned to match global.json (rollForward: latestPatch only accepts 10.0.2xx)
FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Zpi.slnx ./
COPY src/ src/
RUN dotnet restore src/Api/Zpi.Api.csproj
RUN dotnet publish src/Api/Zpi.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Zpi.Api.dll"]
