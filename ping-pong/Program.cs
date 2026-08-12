using System.Globalization;

string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// Shared PersistentVolume path for the running request count — must match
// the same env var on the Log output reader container, with both containers'
// pods mounting the same PVC.
string counterFilePath = Environment.GetEnvironmentVariable("COUNTER_FILE_PATH") ?? "/pv-data/count.log";

string? counterDir = Path.GetDirectoryName(counterFilePath);
if (!string.IsNullOrEmpty(counterDir))
{
    Directory.CreateDirectory(counterDir);
}

// In-memory counter, restored from the persisted file on startup so a pod
// restart continues the count instead of resetting to 0 — otherwise a
// PersistentVolume here wouldn't be any different from an emptyDir.
int counter = 0;
if (File.Exists(counterFilePath) &&
    int.TryParse(File.ReadAllText(counterFilePath).Trim(), out int savedCount))
{
    counter = savedCount;
}

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
        // Bump the in-memory count and persist the new total together, under
        // the same lock, so concurrent requests can't write the file out of
        // order (each write always reflects the latest count when it happens).
        current = counter++;
        File.WriteAllText(counterFilePath, (current + 1).ToString(CultureInfo.InvariantCulture));
    }

    return $"pong {current}";
});

app.Run();
