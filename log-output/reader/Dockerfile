# ---- Build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Reader.csproj .
RUN dotnet restore Reader.csproj

COPY . .
RUN dotnet publish Reader.csproj -c Release -o /app --no-restore

# ---- Runtime ----
# Web app (Kestrel), so this needs aspnet, not the plain runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Run as the non-root user built into the base image (Kubernetes-friendly).
COPY --from=build /app .

# Default port and shared-file location; override FILE_PATH if the volume is
# mounted somewhere else. Must match the writer container's FILE_PATH.
ENV PORT=3000
ENV FILE_PATH=/shared/status.log
EXPOSE $PORT

# Ping-pong's /pongs endpoint, fetched over HTTP. Override if the Service
# name/port/path differs from the default assumption.
ENV PINGPONG_URL=http://ping-pong-svc:2400/pongs

ENTRYPOINT ["dotnet", "Reader.dll"]
