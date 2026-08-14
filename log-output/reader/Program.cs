string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// Must match the writer container's FILE_PATH and both containers' volume
// mount in the pod spec — this is how the two containers actually share data.
string filePath = Environment.GetEnvironmentVariable("FILE_PATH") ?? "/shared/status.log";

// Ping-pong's /pongs endpoint, fetched over HTTP instead of a shared volume.
// Default assumes a "ping-pong-svc" Service on port 2345 (its current
// manifest); override if that name/port/path ever changes.
string pingPongUrl = Environment.GetEnvironmentVariable("PINGPONG_URL") ?? "http://ping-pong-svc:2400/pongs";

// ConfigMap-backed file - e.g. the "information.txt" key of
// log-output-configmap, mounted as a volume in the manifests.
string configFilePath = Environment.GetEnvironmentVariable("CONFIG_FILE_PATH") ?? "/config/information.txt";

using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Keep stdout quiet; this app has nothing it needs to log routinely.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

app.MapGet("/", async () =>
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
        // multi-line dump doesn't combine sensibly with the rest of the reply.
        string[] lines = File.ReadAllLines(filePath);
        lastLine = lines.Length > 0 ? lines[^1] : string.Empty;
    }
    catch (IOException)
    {
        // Rare race with the writer touching the file at the same instant.
        return Results.Text("Status temporarily unavailable, try again.", statusCode: 503);
    }

    // From the ConfigMap: a file (mounted via a volume) and an env var
    // (projected via valueFrom.configMapKeyRef) - both set in the manifests.
    string configFileContent = File.Exists(configFilePath)
        ? File.ReadAllText(configFilePath).Trim()
        : "(config file not found)";
    string message = Environment.GetEnvironmentVariable("MESSAGE") ?? "(not set)";

    string pingPongCount = "unknown";
    try
    {
        // Response looks like "pongs 3" - the count is just the last token.
        string response = await httpClient.GetStringAsync(pingPongUrl);
        string[] parts = response.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            pingPongCount = parts[^1];
        }
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        // Ping-pong may be unreachable or slow to respond — don't fail this
        // whole response over it, just report what couldn't be fetched.
    }

    string body = $"{configFileContent}\nenv variable: MESSAGE={message}\n{lastLine}. Ping / Pongs: {pingPongCount}";
    return Results.Text(body);
});

app.Run();
