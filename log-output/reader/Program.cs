string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// Must match the writer container's FILE_PATH and both containers' volume
// mount in the pod spec — this is how the two containers actually share data.
string filePath = Environment.GetEnvironmentVariable("FILE_PATH") ?? "/shared/status.log";

// Shared PersistentVolume path for the Ping-pong request count — must match
// the same env var on the Ping-pong container, with both pods mounting the
// same PVC.
string counterFilePath = Environment.GetEnvironmentVariable("COUNTER_FILE_PATH") ?? "/pv-data/count.log";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Keep stdout quiet; this app has nothing it needs to log routinely.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

app.MapGet("/", () =>
{
    if (!File.Exists(filePath))
    {
        // Writer may not have written its first line yet (or the volume isn't
        // mounted where expected) — fail loudly rather than pretend success.
        return Results.Text("No status written yet.", statusCode: 503);
    }

    string lastLine;
    try
    {
        // Only the latest entry — this is a "current status" endpoint, and a
        // multi-line dump doesn't combine sensibly with the ping-pong count.
        string[] lines = File.ReadAllLines(filePath);
        lastLine = lines.Length > 0 ? lines[^1] : string.Empty;
    }
    catch (IOException)
    {
        // Rare race with the writer touching the file at the same instant.
        return Results.Text("Status temporarily unavailable, try again.", statusCode: 503);
    }

    string pingPongCount = "unknown";
    if (File.Exists(counterFilePath))
    {
        try
        {
            pingPongCount = File.ReadAllText(counterFilePath).Trim();
        }
        catch (IOException)
        {
            // Leave it as "unknown" rather than fail the whole response over this.
        }
    }

    return Results.Text($"{lastLine}. Ping / Pongs: {pingPongCount}");
});

app.Run();
