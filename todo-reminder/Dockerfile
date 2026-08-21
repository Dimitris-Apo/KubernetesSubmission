# ---- Build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer first, so it's only re-run when the csproj changes.
COPY TodoReminder.csproj .
RUN dotnet restore TodoReminder.csproj

# Copy the rest of the source and publish a self-contained Release build.
COPY . .
RUN dotnet publish TodoReminder.csproj -c Release -o /app --no-restore

# ---- Runtime ----
# No web server here, so the plain runtime image (not aspnet) is enough.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Run as the non-root user built into the base image (Kubernetes-friendly).
USER $APP_UID
COPY --from=build /app .

ENV TODO_BACKEND_URL=http://todo-backend-svc:2345/todos
ENV WIKIPEDIA_RANDOM_URL=https://en.wikipedia.org/api/rest_v1/page/random/summary

ENTRYPOINT ["dotnet", "TodoReminder.dll"]
