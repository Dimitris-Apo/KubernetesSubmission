using System.Globalization;

// Generate and store the random string in memory on startup.
string uniqueId = Guid.NewGuid().ToString();

// Where the shared file lives — must match the reader's FILE_PATH and both
// containers' volume mount in the pod spec.
string filePath = Environment.GetEnvironmentVariable("FILE_PATH") ?? "/shared/status.log";

string? dir = Path.GetDirectoryName(filePath);
if (!string.IsNullOrEmpty(dir))
{
    Directory.CreateDirectory(dir);
}

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

void WriteEntry()
{
    string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "Z";
    File.AppendAllText(filePath, $"{timestamp}: {uniqueId}{Environment.NewLine}");
}

try
{
    // Write immediately on startup, then every 5 seconds thereafter.
    WriteEntry();

    using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        WriteEntry();
    }
}
catch (OperationCanceledException)
{
    // Graceful shutdown (Ctrl+C / SIGINT).
}
