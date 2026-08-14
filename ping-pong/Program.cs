string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// In-memory request counter. Log output now reads this over HTTP via
// /pongs instead of a shared volume.
int counter = 0;
object counterLock = new();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// This app has nothing worth logging routinely; keep stdout quiet.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

// Registered at /pingpong (not /) since it's meant to sit behind an Ingress
// that forwards that path as-is, alongside a sibling app on other paths.
app.MapGet("/pingpong", () =>
{
    int current;
    lock (counterLock)
    {
        current = counter++;
    }

    return $"pong {current}";
});

// Log output polls this over HTTP to get the running total.
app.MapGet("/pongs", () =>
{
    int current;
    lock (counterLock)
    {
        current = counter;
    }

    return $"pongs {current}";
});

app.Run();
