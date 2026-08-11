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
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Run as the non-root user built into the base image (Kubernetes-friendly).
USER $APP_UID
COPY --from=build /app .

ENTRYPOINT ["dotnet", "KubernetesProject.dll"]
