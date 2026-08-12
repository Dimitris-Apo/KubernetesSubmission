// See https://aka.ms/new-console-template for more information
using System.Globalization;

// 1. Generate and store the random string in memory on startup
string uniqueId = Guid.NewGuid().ToString();

string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Keep stdout as a clean log stream: drop Kestrel's routine request/lifecycle
// noise, but keep warnings/errors visible.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

// Same "timestamp: id" format used by both the periodic stdout log and the HTTP endpoint.
string FormatEntry() =>
    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "Z: " + uniqueId;

// 2. Keep printing to stdout immediately and then every 5 seconds, in the background,
//    alongside the HTTP server. Stops cleanly on shutdown (Ctrl+C/SIGTERM).
CancellationToken stoppingToken = app.Lifetime.ApplicationStopping;
_ = Task.Run(async () =>
{
    try
    {
        Console.WriteLine(FormatEntry());
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            Console.WriteLine(FormatEntry());
        }
    }
    catch (OperationCanceledException)
    {
        // Expected on shutdown.
    }
});

// 3. On-demand HTTP endpoint reporting the current status
//    (current timestamp + the random string stored in memory since startup).
app.MapGet("/", () => FormatEntry());

app.Run();
