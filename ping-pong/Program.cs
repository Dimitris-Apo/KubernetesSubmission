using Npgsql;

string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// Postgres connection details - all overridable via env vars. DB_PASSWORD
// has no fallback on purpose: there's no sensible default for a credential.
string dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
string dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
string dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
string dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
string dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

string connectionString =
    $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

// Make sure the counter table (and its single row) exist before serving any
// requests. If Postgres isn't reachable yet, fail fast here rather than
// start "successfully" and only break on the first request - Kubernetes
// will just restart the pod until the database is up.
await using (NpgsqlConnection initConn = new(connectionString))
{
    await initConn.OpenAsync();

    await using NpgsqlCommand createTable = new(
        "CREATE TABLE IF NOT EXISTS pingpong_counter (id INT PRIMARY KEY, value INT NOT NULL)",
        initConn);
    await createTable.ExecuteNonQueryAsync();

    await using NpgsqlCommand seedRow = new(
        "INSERT INTO pingpong_counter (id, value) VALUES (1, 0) ON CONFLICT (id) DO NOTHING",
        initConn);
    await seedRow.ExecuteNonQueryAsync();
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// This app has nothing worth logging routinely; keep stdout quiet.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

// Registered at /pingpong (not /) since it's meant to sit behind an Ingress
// that forwards that path as-is, alongside a sibling app on other paths.
app.MapGet("/pingpong", async () =>
{
    // A single atomic UPDATE...RETURNING does the increment-and-read in one
    // step - Postgres's own row locking handles concurrent requests safely,
    // no application-level lock needed anymore.
    await using NpgsqlConnection conn = new(connectionString);
    await conn.OpenAsync();
    await using NpgsqlCommand cmd = new(
        "UPDATE pingpong_counter SET value = value + 1 WHERE id = 1 RETURNING value",
        conn);
    object? result = await cmd.ExecuteScalarAsync();
    int newTotal = Convert.ToInt32(result);
    int current = newTotal - 1; // report the pre-increment value, as before

    return $"pong {current}";
});

// Log output polls this over HTTP to get the running total. Response format
// is unchanged, so nothing on the log-output side needs to know this moved
// from memory to a database.
app.MapGet("/pongs", async () =>
{
    await using NpgsqlConnection conn = new(connectionString);
    await conn.OpenAsync();
    await using NpgsqlCommand cmd = new("SELECT value FROM pingpong_counter WHERE id = 1", conn);
    object? result = await cmd.ExecuteScalarAsync();
    int current = result is null ? 0 : Convert.ToInt32(result);

    return $"pongs {current}";
});

app.Run();
