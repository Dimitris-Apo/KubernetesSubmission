# ---- Build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer first, so it's only re-run when the csproj changes.
COPY TodoApp.csproj .
RUN dotnet restore TodoApp.csproj

# Copy the rest of the source and publish a self-contained Release build.
COPY . .
RUN dotnet publish TodoApp.csproj -c Release -o /app --no-restore

# ---- Runtime ----
# Note: aspnet, not plain dotnet/runtime — this is a web app and needs the
# ASP.NET Core shared framework (Kestrel etc.), which the bare runtime image lacks.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app .

# Default/documented port; actual port at runtime is controlled by the PORT
# env var (the app itself also falls back to 3000 if it's unset).
ENV PORT=3000
EXPOSE $PORT

# Default cached-image location; override IMAGE_CACHE_PATH if the volume is
# mounted somewhere else.
ENV IMAGE_CACHE_PATH=/image-cache/picture.jpg

# How long a cached picture is served before a background refresh is triggered.
ENV IMAGE_CACHE_MINUTES=10

# Where the random picture itself comes from.
ENV PICSUM_URL=https://picsum.photos/1200

ENTRYPOINT ["dotnet", "TodoApp.dll"]
