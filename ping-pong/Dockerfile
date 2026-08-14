# ---- Build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer first, so it's only re-run when the csproj changes.
COPY PingPong.csproj .
RUN dotnet restore PingPong.csproj

# Copy the rest of the source and publish a self-contained Release build.
COPY . .
RUN dotnet publish PingPong.csproj -c Release -o /app --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Run as the non-root user built into the base image (Kubernetes-friendly).
# Safe again now that this app no longer writes to a mounted volume.
USER $APP_UID
COPY --from=build /app .

# Default/documented port; actual port at runtime is controlled by the PORT
# env var (the app itself also falls back to 3000 if it's unset).
ENV PORT=3000
EXPOSE $PORT

ENTRYPOINT ["dotnet", "PingPong.dll"]
