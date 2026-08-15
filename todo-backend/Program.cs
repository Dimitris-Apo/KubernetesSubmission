using Npgsql;

string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// Was a hardcoded const, now configurable too.
int maxTodoLength = int.TryParse(Environment.GetEnvironmentVariable("MAX_TODO_LENGTH"), out int parsedMaxLength)
    ? parsedMaxLength
    : 140;

// Postgres connection details - all overridable via env vars, same pattern
// as ping-pong. DB_PASSWORD has no fallback on purpose: there's no sensible
// default for a credential.
string dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
string dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
string dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
string dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
string dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

string connectionString =
    $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

// Make sure the todos table exists (and is seeded once) before serving any
// requests. If Postgres isn't reachable yet, fail fast here rather than
// start "successfully" and only break on the first request - Kubernetes
// will just restart the pod until the database is up.
await using (NpgsqlConnection initConn = new(connectionString))
{
    await initConn.OpenAsync();

    await using NpgsqlCommand createTable = new(
        "CREATE TABLE IF NOT EXISTS todos (id SERIAL PRIMARY KEY, text TEXT NOT NULL)",
        initConn);
    await createTable.ExecuteNonQueryAsync();

    await using NpgsqlCommand countCmd = new("SELECT COUNT(*) FROM todos", initConn);
    long existingCount = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);
    if (existingCount == 0)
    {
        // Same starter items the in-memory version used to seed - only once,
        // the first time the table is created.
        await using NpgsqlCommand seed = new(
            "INSERT INTO todos (text) VALUES ('Learn Docker'), ('Learn Kubernetes'), ('Water the plants')",
            initConn);
        await seed.ExecuteNonQueryAsync();
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Keep stdout quiet; this app has nothing it needs to log routinely.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Permissive CORS: in the intended setup, todo-app and todo-backend sit
// behind the same Ingress on different paths, so browser requests are
// same-origin and this never comes into play. Left on defensively for any
// setup where they end up on different origins (e.g. local dev without an
// Ingress in front).
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/todos", async () =>
{
    List<Todo> todos = [];

    await using NpgsqlConnection conn = new(connectionString);
    await conn.OpenAsync();
    await using NpgsqlCommand cmd = new("SELECT id, text FROM todos ORDER BY id", conn);
    await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        todos.Add(new Todo(reader.GetInt32(0), reader.GetString(1)));
    }

    return Results.Ok(todos);
});

app.MapPost("/todos", async (TodoInput input) =>
{
    string text = input.Text?.Trim() ?? string.Empty;
    if (string.IsNullOrEmpty(text) || text.Length > maxTodoLength)
    {
        return Results.BadRequest(new { error = $"text must be 1-{maxTodoLength} characters" });
    }

    await using NpgsqlConnection conn = new(connectionString);
    await conn.OpenAsync();
    await using NpgsqlCommand cmd = new("INSERT INTO todos (text) VALUES (@text) RETURNING id, text", conn);
    cmd.Parameters.AddWithValue("text", text);
    await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();
    Todo created = new(reader.GetInt32(0), reader.GetString(1));

    return Results.Created($"/todos/{created.Id}", created);
});

app.Run();

record Todo(int Id, string Text);

record TodoInput(string? Text);
