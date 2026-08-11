// See https://aka.ms/new-console-template for more information
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

// 1. Generate and store the random string in memory on startup
string uniqueId = Guid.NewGuid().ToString();

// Handle graceful shutdown (Ctrl+C)
using CancellationTokenSource cts = new();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// 2. Output the string with an ISO-8601 UTC timestamp, e.g. 2020-03-30T12:15:17.705Z: <string>
void PrintEntry()
{
    string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "Z";
    Console.WriteLine($"{timestamp}: {uniqueId}");
}

try
{
    // 3. Print immediately on startup, then every 5 seconds thereafter
    PrintEntry();

    using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        PrintEntry();
    }
}
catch (OperationCanceledException)
{
    // Keep stdout as a clean log stream; shutdown notice goes to stderr instead.
    Console.Error.WriteLine("Application stopped.");
}