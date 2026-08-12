# ---- Build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer first, so it's only re-run when the csproj changes.
COPY KubernetesProject.csproj .
RUN dotnet restore KubernetesProject.csproj

# Copy the rest of the source and publish a self-contained Release build.
COPY . .
RUN dotnet publish KubernetesProject.csproj -c Release -o /app --no-restore

# ---- Runtime ----
# Note: aspnet, not plain dotnet/runtime — this is now a web app and needs the
# ASP.NET Core shared framework (Kestrel etc.), which the bare runtime image lacks.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Run as the non-root user built into the base image (Kubernetes-friendly).
USER $APP_UID
COPY --from=build /app .

# Default/documented port; actual port at runtime is controlled by the PORT
# env var (the app itself also falls back to 3000 if it's unset).
ENV PORT=3000
EXPOSE $PORT

ENTRYPOINT ["dotnet", "KubernetesProject.dll"]
