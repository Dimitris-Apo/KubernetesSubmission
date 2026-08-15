# ---- Build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer first, so it's only re-run when the csproj changes.
COPY TodoBackend.csproj .
RUN dotnet restore TodoBackend.csproj

# Copy the rest of the source and publish a self-contained Release build.
COPY . .
RUN dotnet publish TodoBackend.csproj -c Release -o /app --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Run as the non-root user built into the base image (Kubernetes-friendly).
# No volume writes here, so this is safe to keep non-root.
USER $APP_UID
COPY --from=build /app .

# Default/documented port; actual port at runtime is controlled by the PORT
# env var (the app itself also falls back to 3000 if it's unset).
ENV PORT=3000
EXPOSE $PORT

# Maximum allowed length for a todo's text (enforced server-side).
ENV MAX_TODO_LENGTH=140

# Postgres connection - override for your actual cluster setup. No default
# for DB_PASSWORD on purpose; that one should come from a Secret.
ENV DB_HOST=postgres-svc
ENV DB_PORT=5432
ENV DB_NAME=postgres
ENV DB_USER=postgres

ENTRYPOINT ["dotnet", "TodoBackend.dll"]
