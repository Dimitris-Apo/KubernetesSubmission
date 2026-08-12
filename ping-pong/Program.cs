// In-memory request counter. Lives only as long as this process does —
// a pod restart resets it back to 0, which is expected.
int counter = 0;

string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// This app has nothing worth logging routinely; keep stdout quiet.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

// Registered at /pingpong (not /) since it's meant to sit behind an Ingress
// that forwards that path as-is, alongside a sibling app on other paths.
app.MapGet("/pingpong", () =>
{
    // Atomically read-then-increment: this request gets the current count,
    // the next caller gets the incremented one. Safe under concurrent hits.
    int current = Interlocked.Increment(ref counter) - 1;
    return $"pong {current}";
});

app.Run();
