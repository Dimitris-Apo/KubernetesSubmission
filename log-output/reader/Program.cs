string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// Must match the writer container's FILE_PATH and both containers' volume
// mount in the pod spec — this is how the two containers actually share data.
string filePath = Environment.GetEnvironmentVariable("FILE_PATH") ?? "/shared/status.log";

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

    try
    {
        string content = File.ReadAllText(filePath);
        return Results.Text(content);
    }
    catch (IOException)
    {
        // Rare race with the writer touching the file at the same instant.
        return Results.Text("Status temporarily unavailable, try again.", statusCode: 503);
    }
});

app.Run();
