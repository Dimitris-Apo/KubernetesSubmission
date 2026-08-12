# ---- Build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Writer.csproj .
RUN dotnet restore Writer.csproj

COPY . .
RUN dotnet publish Writer.csproj -c Release -o /app --no-restore

# ---- Runtime ----
# No web server here, so the plain runtime image (not aspnet) is enough.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Run as the non-root user built into the base image (Kubernetes-friendly).
COPY --from=build /app .

# Default shared-file location; override via FILE_PATH if the volume is
# mounted somewhere else. Must match the reader container's FILE_PATH.
ENV FILE_PATH=/shared/status.log

ENTRYPOINT ["dotnet", "Writer.dll"]
